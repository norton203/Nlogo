using System.Text.Json.Serialization;

namespace Nlogo.Models;

// ══════════════════════════════════════════════════════════════════
//  .logowork file format  (student → teacher offline submission)
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Root object serialised to / from a .logowork file.
/// </summary>
public sealed class LogoWorkPackage
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; }

    [JsonPropertyName("student")]
    public StudentInfo Student { get; set; } = new();

    [JsonPropertyName("submissions")]
    public List<SubmissionRecord> Submissions { get; set; } = [];

    /// <summary>
    /// SHA-256 hash of the serialised Submissions list.
    /// Lets the teacher's app warn if the file was hand-edited.
    /// </summary>
    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;
}

public sealed class StudentInfo
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("classInviteCode")]
    public string ClassInviteCode { get; set; } = string.Empty;
}

public sealed class SubmissionRecord
{
    [JsonPropertyName("challengeId")]
    public string ChallengeId { get; set; } = string.Empty;

    [JsonPropertyName("challengeName")]
    public string ChallengeName { get; set; } = string.Empty;

    [JsonPropertyName("completedAt")]
    public DateTime CompletedAt { get; set; }

    [JsonPropertyName("timeTakenSeconds")]
    public long TimeTakenSeconds { get; set; }

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("stars")]
    public int Stars { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }
}

// ══════════════════════════════════════════════════════════════════
//  Import result  (returned after teacher imports a .logowork file)
// ══════════════════════════════════════════════════════════════════

public sealed class ImportResult
{
    public bool Success { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public int SubmissionCount { get; init; }
    public int DuplicatesSkipped { get; init; }
    public bool TamperedWarning { get; init; }
    public bool UnmatchedClass { get; init; }
    public string? ErrorMessage { get; init; }

    public static ImportResult Failed(string reason) =>
        new() { Success = false, ErrorMessage = reason };
}

// ══════════════════════════════════════════════════════════════════
//  Imported record stored in-memory on teacher's side
// ══════════════════════════════════════════════════════════════════

public sealed class ImportedSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentDeviceId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ClassInviteCode { get; set; } = string.Empty;
    public string ChallengeId { get; set; } = string.Empty;
    public string ChallengeName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public long TimeTakenSeconds { get; set; }
    public int Attempts { get; set; }
    public int Stars { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public bool ImportedViaEmail { get; set; }
    public bool ChecksumValid { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
}

// ══════════════════════════════════════════════════════════════════
//  Student settings (persisted via Preferences API)
// ══════════════════════════════════════════════════════════════════

public sealed class StudentSettings
{
    public string StudentName { get; set; } = string.Empty;
    public string TeacherEmail { get; set; } = string.Empty;
    public string ClassInviteCode { get; set; } = string.Empty;
}