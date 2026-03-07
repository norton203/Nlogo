using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Teacher-side service. Parses .logowork files received via email or
/// file picker, validates the checksum, deduplicates, and holds all
/// imported submissions in memory for the marking dashboard.
/// </summary>
public sealed class ImportService
{
    // In-memory store (no SQLite yet — easy to swap later)
    private readonly List<ImportedSubmission> _submissions = [];

    public event Action? SubmissionsChanged;

    // ── Public queries ─────────────────────────────────────────────────────

    public IReadOnlyList<ImportedSubmission> All => _submissions.AsReadOnly();

    public IReadOnlyList<string> StudentNames =>
        _submissions.Select(s => s.StudentName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

    public IReadOnlyList<ImportedSubmission> ForStudent(string name) =>
        _submissions.Where(s => s.StudentName == name)
                    .OrderBy(s => s.ChallengeId)
                    .ToList();

    // ── Import from file path ──────────────────────────────────────────────

    public async Task<ImportResult> ImportFromPathAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            return await ProcessJsonAsync(json);
        }
        catch (Exception ex)
        {
            return ImportResult.Failed($"Could not read file: {ex.Message}");
        }
    }

    // ── Import from raw JSON string ────────────────────────────────────────

    public async Task<ImportResult> ProcessJsonAsync(string json)
    {
        LogoWorkPackage package;
        try
        {
            package = JsonSerializer.Deserialize<LogoWorkPackage>(json)
                      ?? throw new JsonException("Null result");
        }
        catch (Exception ex)
        {
            return ImportResult.Failed($"File is not a valid .logowork file: {ex.Message}");
        }

        // ── Validate checksum ──────────────────────────────────────────────
        var submissionsJson = JsonSerializer.Serialize(
            package.Submissions,
            new JsonSerializerOptions { WriteIndented = true });

        var expectedHash = $"sha256:{ComputeSha256(submissionsJson)}";
        var tampered = !string.Equals(
            package.Checksum, expectedHash,
            StringComparison.OrdinalIgnoreCase);

        // ── Process records ────────────────────────────────────────────────
        int added = 0;
        int duplicates = 0;

        foreach (var sub in package.Submissions)
        {
            // Deduplicate by deviceId + challengeId + completedAt
            bool exists = _submissions.Any(s =>
                s.StudentDeviceId == package.Student.DeviceId &&
                s.ChallengeId     == sub.ChallengeId          &&
                s.CompletedAt     == sub.CompletedAt);

            if (exists)
            {
                duplicates++;
                continue;
            }

            _submissions.Add(new ImportedSubmission
            {
                StudentDeviceId  = package.Student.DeviceId,
                StudentName      = package.Student.Name,
                ClassInviteCode  = package.Student.ClassInviteCode,
                ChallengeId      = sub.ChallengeId,
                ChallengeName    = sub.ChallengeName,
                CompletedAt      = sub.CompletedAt,
                TimeTakenSeconds = sub.TimeTakenSeconds,
                Attempts         = sub.Attempts,
                Stars            = sub.Stars,
                Code             = sub.Code,
                Passed           = sub.Passed,
                ImportedViaEmail = true,
                ChecksumValid    = !tampered,
                ImportedAt       = DateTime.Now
            });

            added++;
        }

        SubmissionsChanged?.Invoke();

        return new ImportResult
        {
            Success           = true,
            StudentName       = package.Student.Name,
            SubmissionCount   = added,
            DuplicatesSkipped = duplicates,
            TamperedWarning   = tampered,
            // UnmatchedClass would be checked against a class list once the
            // full teacher portal / network layer is implemented.
            UnmatchedClass    = false
        };
    }

    // ── File picker ────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the platform file picker filtered to .logowork files
    /// and imports the selected file.
    /// </summary>
    public async Task<ImportResult?> PickAndImportAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select a .logowork submission file",
                FileTypes   = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI,   [".logowork"] },
                        { DevicePlatform.macOS,   ["logowork"]  },
                        { DevicePlatform.Android, ["application/octet-stream"] },
                        { DevicePlatform.iOS,     ["public.data"] }
                    })
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result is null) return null; // user cancelled

            return await ImportFromPathAsync(result.FullPath);
        }
        catch (Exception ex)
        {
            return ImportResult.Failed($"File picker error: {ex.Message}");
        }
    }

    // ── Clear ──────────────────────────────────────────────────────────────

    public void ClearAll()
    {
        _submissions.Clear();
        SubmissionsChanged?.Invoke();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}