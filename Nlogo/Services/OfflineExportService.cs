using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Builds a .logowork submission package from current challenge progress,
/// writes it to the app cache directory, then opens the native email or
/// share sheet so the student can send it to their teacher.
/// </summary>
public sealed class OfflineExportService
{
    private readonly ChallengeService _challenges;
    private readonly StudentSettingsService _settings;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true
    };

    public OfflineExportService(
        ChallengeService challenges,
        StudentSettingsService settings)
    {
        _challenges = challenges;
        _settings   = settings;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates the .logowork file and returns its path.
    /// Throws if the student has no completed challenges.
    /// </summary>
    public async Task<string> GeneratePackageAsync()
    {
        var cfg = _settings.Load();

        // Collect all progress that has at least 1 star
        var submissions = new List<SubmissionRecord>();

        foreach (var cat in _challenges.Catalog)
        {
            foreach (var ch in cat.Challenges)
            {
                var prog = _challenges.GetProgress(ch.Id);
                if (prog is null || prog.Stars == 0) continue;

                submissions.Add(new SubmissionRecord
                {
                    ChallengeId      = ch.Id,
                    ChallengeName    = ch.Title,
                    CompletedAt      = prog.CompletedAt,
                    TimeTakenSeconds = (long)prog.BestTime.TotalSeconds,
                    Stars            = prog.Stars,
                    // Attempts not tracked yet - placeholder
                    Attempts         = 1,
                    // Code not stored in ChallengeProgress yet - we'll add a
                    // LastCode field to SaveProgress in a later pass; for now
                    // we store a placeholder so the format is future-proof.
                    Code             = prog.LastCode ?? string.Empty,
                    Passed           = prog.Stars > 0
                });
            }
        }

        if (submissions.Count == 0)
            throw new InvalidOperationException(
                "No completed challenges found. Complete at least one challenge before submitting.");

        // Build the package
        var package = new LogoWorkPackage
        {
            Version    = "1.0",
            ExportedAt = DateTime.UtcNow,
            Student    = new StudentInfo
            {
                DeviceId        = GetDeviceId(),
                Name            = cfg.StudentName,
                ClassInviteCode = cfg.ClassInviteCode
            },
            Submissions = submissions
        };

        // Compute tamper-check hash BEFORE adding it to the object
        var submissionsJson = JsonSerializer.Serialize(submissions, _jsonOpts);
        package.Checksum    = $"sha256:{ComputeSha256(submissionsJson)}";

        // Serialise
        var json = JsonSerializer.Serialize(package, _jsonOpts);
        var safeName = SanitiseFileName(cfg.StudentName.Length > 0 ? cfg.StudentName : "Student");
        var fileName = $"submission_{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.logowork";
        var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        return filePath;
    }

    /// <summary>
    /// Generates the package and opens the native email client with it
    /// pre-attached. Falls back to the native share sheet if no email
    /// client is available.
    /// </summary>
    public async Task<EmailSendResult> SendViaEmailAsync()
    {
        string filePath;
        try
        {
            filePath = await GeneratePackageAsync();
        }
        catch (InvalidOperationException ex)
        {
            return new EmailSendResult(false, ex.Message);
        }

        var cfg = _settings.Load();

        try
        {
            var message = new EmailMessage
            {
                Subject = BuildSubject(cfg.StudentName),
                Body    = BuildBody(cfg.StudentName),
                To      = cfg.TeacherEmail.Length > 0
                             ? [cfg.TeacherEmail]
                             : []
            };

            message.Attachments.Add(new EmailAttachment(filePath));
            await Email.Default.ComposeAsync(message);

            return new EmailSendResult(true, null, filePath);
        }
        catch (FeatureNotSupportedException)
        {
            // No email client — use the native share sheet instead
            return await ShareFileAsync(filePath);
        }
    }

    /// <summary>
    /// Opens the native share sheet (Teams, WhatsApp, AirDrop, etc.) with
    /// the generated package — useful when no email client is configured.
    /// </summary>
    public async Task<EmailSendResult> ShareViaShareSheetAsync()
    {
        string filePath;
        try
        {
            filePath = await GeneratePackageAsync();
        }
        catch (InvalidOperationException ex)
        {
            return new EmailSendResult(false, ex.Message);
        }

        return await ShareFileAsync(filePath);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<EmailSendResult> ShareFileAsync(string filePath)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Logo Work Submission",
                File  = new ShareFile(filePath)
            });
            return new EmailSendResult(true, null, filePath);
        }
        catch (Exception ex)
        {
            return new EmailSendResult(false, $"Could not share file: {ex.Message}");
        }
    }

    private static string BuildSubject(string name) =>
        $"Logo Work Submission — {(name.Length > 0 ? name : "Student")} — {DateTime.Now:dd MMM yyyy}";

    private static string BuildBody(string name) =>
        $"Please find my Logo programming submission attached.\n\n" +
        $"Student : {(name.Length > 0 ? name : "(name not set)")}\n" +
        $"Date    : {DateTime.Now:dd MMMM yyyy}\n\n" +
        $"Submitted via Nlogo IDE";

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDeviceId()
    {
        // MAUI DeviceInfo doesn't give a persistent unique ID on all platforms;
        // we combine name + model as a readable fingerprint.
        return $"{DeviceInfo.Current.Name}_{DeviceInfo.Current.Model}";
    }

    private static string SanitiseFileName(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
}

/// <summary>Result of an email / share attempt.</summary>
public sealed record EmailSendResult(
    bool Success,
    string? ErrorMessage,
    string? FilePath = null);