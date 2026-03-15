using System.Security.Cryptography;
using System.Text;

namespace Nlogo.Services;

/// <summary>
/// Handles wrapping and unwrapping of .nlogo source files.
///
/// File format:
///   #NLOGO 1.0
///   #saved: 2026-03-14T10:30:00Z
///   #checksum: sha256:&lt;hex&gt;
///   #---
///   &lt;raw Logo source code&gt;
///
/// The magic header line makes the file unambiguously identifiable.
/// The SHA-256 checksum lets the IDE warn if the file was hand-edited
/// outside the app (same pattern used in .logowork submission files).
/// </summary>
public static class NLogoFileFormat
{
    // ── Constants ──────────────────────────────────────────────────────────

    public const string Extension = ".nlogo";
    public const string LegacyExt = ".logo";       // accepted on open, not on save
    public const string Magic = "#NLOGO";
    public const string Version = "1.0";
    private const string Separator = "#---";

    // ── Wrap ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps raw Logo source code in the .nlogo file envelope.
    /// </summary>
    public static string Wrap(string code)
    {
        var checksum = ComputeChecksum(code);
        var sb = new StringBuilder();
        sb.AppendLine($"{Magic} {Version}");
        sb.AppendLine($"#saved: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine($"#checksum: sha256:{checksum}");
        sb.AppendLine(Separator);
        sb.Append(code);
        return sb.ToString();
    }

    // ── Unwrap ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the content of a .nlogo or .logo file.
    /// Returns the raw Logo source and any warning message.
    /// Returns null for <paramref name="code"/> if the file is invalid.
    /// </summary>
    public static UnwrapResult Unwrap(string content, string fileExtension)
    {
        // Legacy .logo files: plain text, no header expected — load as-is.
        if (string.Equals(fileExtension, LegacyExt, StringComparison.OrdinalIgnoreCase))
            return new UnwrapResult(true, content.TrimEnd(), null);

        var lines = content.Split('\n');

        // ── Validate magic header ──────────────────────────────────────────
        if (lines.Length == 0 ||
            !lines[0].TrimEnd().StartsWith($"{Magic} ", StringComparison.Ordinal))
        {
            return new UnwrapResult(false, null,
                $"Not a valid NLogo file — missing '{Magic}' header.\n" +
                 "This file may be corrupt or was not saved by the Logo IDE.");
        }

        // ── Locate separator ───────────────────────────────────────────────
        int sepIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == Separator)
            {
                sepIndex = i;
                break;
            }
        }

        if (sepIndex < 0)
        {
            return new UnwrapResult(false, null,
                "Corrupted NLogo file — the content separator is missing.");
        }

        // ── Extract code ───────────────────────────────────────────────────
        var code = string.Join('\n', lines.Skip(sepIndex + 1));

        // ── Validate checksum (non-fatal — warn but still load) ────────────
        string? warning = null;

        var checksumLine = lines
            .Take(sepIndex)
            .FirstOrDefault(l => l.StartsWith("#checksum: sha256:", StringComparison.Ordinal));

        if (checksumLine is not null)
        {
            var storedHash = checksumLine["#checksum: sha256:".Length..].Trim();
            var actualHash = ComputeChecksum(code);

            if (!string.Equals(storedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                warning =
                    "⚠️ Checksum mismatch — this file may have been edited outside the IDE.\n" +
                    "The code has been loaded, but please verify it looks correct.";
            }
        }

        return new UnwrapResult(true, code.TrimEnd(), warning);
    }

    // ── IsNLogoFile ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the extension is .nlogo or (legacy) .logo.
    /// </summary>
    public static bool IsSupported(string path) =>
        string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), LegacyExt, StringComparison.OrdinalIgnoreCase);

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string ComputeChecksum(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ── Result record ──────────────────────────────────────────────────────────

/// <summary>
/// The result of <see cref="NLogoFileFormat.Unwrap"/>.
/// </summary>
/// <param name="Valid">False means the file was not recognised at all.</param>
/// <param name="Code">The raw Logo source, or null if invalid.</param>
/// <param name="Warning">Non-null means "loaded but with a caveat" (e.g. tampered).</param>
public sealed record UnwrapResult(bool Valid, string? Code, string? Warning);