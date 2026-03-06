using System.Text.Json;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Handles reading and writing challenge progress to a JSON file
/// stored in the app's private data directory.
/// File: {AppDataDirectory}/challenge_progress.json
/// </summary>
public sealed class ProgressPersistenceService
{
    private static readonly string FilePath =
        Path.Combine(FileSystem.AppDataDirectory, "challenge_progress.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    // ── Load ───────────────────────────────────────────────────────────────
    public async Task<Dictionary<string, ChallengeProgress>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new();

            var json = await File.ReadAllTextAsync(FilePath);
            var dtos = JsonSerializer.Deserialize<List<ProgressDto>>(json, Opts) ?? [];

            return dtos.ToDictionary(
                d => d.ChallengeId,
                d => new ChallengeProgress(
                    d.ChallengeId,
                    d.Stars,
                    TimeSpan.FromTicks(d.BestTimeTicks),
                    new DateTime(d.CompletedAtTicks)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Progress] Load failed: {ex.Message}");
            return new();
        }
    }

    // ── Save ───────────────────────────────────────────────────────────────
    public async Task SaveAsync(IEnumerable<ChallengeProgress> progress)
    {
        try
        {
            var dtos = progress
                .Select(p => new ProgressDto(
                    p.ChallengeId,
                    p.Stars,
                    p.BestTime.Ticks,
                    p.CompletedAt.Ticks))
                .ToList();

            var json = JsonSerializer.Serialize(dtos, Opts);

            // Write to temp file first, then atomically replace — prevents
            // corruption if the app is killed mid-write.
            var tmp = FilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json);
            File.Move(tmp, FilePath, overwrite: true);

            System.Diagnostics.Debug.WriteLine($"[Progress] Saved {dtos.Count} entries.");
        }
        catch (Exception ex)
        {
            // Progress loss is non-fatal — swallow and log only.
            System.Diagnostics.Debug.WriteLine($"[Progress] Save failed: {ex.Message}");
        }
    }

    // ── DTO ────────────────────────────────────────────────────────────────
    // Records with primitive types only — no TimeSpan/DateTime in JSON directly.
    private sealed record ProgressDto(
        string ChallengeId,
        int Stars,
        long BestTimeTicks,
        long CompletedAtTicks);
}