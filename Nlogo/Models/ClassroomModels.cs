namespace Nlogo.Models;

// ══════════════════════════════════════════════════════════════════
//  Live classroom — shared models (teacher hub ↔ student client)
// ══════════════════════════════════════════════════════════════════

/// <summary>
/// Snapshot of a connected student's progress, broadcast to all
/// teacher dashboard listeners whenever anything changes.
/// </summary>
public sealed class StudentStatus
{
    public string ConnectionId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int TotalStars { get; set; }
    public int CompletedCount { get; set; }

    /// <summary>ID of the challenge the student is currently working on.</summary>
    public string CurrentChallengeId { get; set; } = string.Empty;
    public string CurrentChallengeName { get; set; } = string.Empty;

    public DateTime JoinedAt { get; set; } = DateTime.Now;
    public DateTime LastActivity { get; set; } = DateTime.Now;

    /// <summary>True while the student has been seen within the last 30 s.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Result returned to the student after a JoinClass hub call.
/// </summary>
public sealed class JoinResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}