using Microsoft.AspNetCore.SignalR;
using Nlogo.Models;
using System.Collections.Concurrent;

namespace Nlogo.Services;

/// <summary>
/// SignalR hub that runs on the teacher's machine.
/// Students connect and call JoinClass / UpdateProgress.
/// The hub broadcasts the live roster to all teacher listeners.
/// </summary>
public sealed class ClassroomHub : Hub
{
    private static readonly ConcurrentDictionary<string, StudentStatus> _students = new();

    // ── Client → Hub calls (students call these) ───────────────────────────

    public async Task<JoinResult> JoinClass(string studentName)
    {
        if (string.IsNullOrWhiteSpace(studentName))
            return new JoinResult { Success = false, ErrorMessage = "Name cannot be empty." };

        var status = new StudentStatus
        {
            ConnectionId  = Context.ConnectionId,
            StudentName   = studentName.Trim(),
            JoinedAt      = DateTime.Now,
            LastActivity  = DateTime.Now
        };

        _students[Context.ConnectionId] = status;

        // Add to teacher group so teachers get all future updates
        await Groups.AddToGroupAsync(Context.ConnectionId, "students");

        // Notify all teacher dashboards
        await Clients.Group("teachers").SendAsync("RosterChanged", GetRoster());

        return new JoinResult
        {
            Success      = true,
            StudentCount = _students.Count
        };
    }

    public async Task UpdateProgress(
        string currentChallengeId,
        string currentChallengeName,
        int totalStars,
        int completedCount)
    {
        if (!_students.TryGetValue(Context.ConnectionId, out var status))
            return;

        status.CurrentChallengeId   = currentChallengeId;
        status.CurrentChallengeName = currentChallengeName;
        status.TotalStars           = totalStars;
        status.CompletedCount       = completedCount;
        status.LastActivity         = DateTime.Now;
        status.IsActive             = true;

        await Clients.Group("teachers").SendAsync("RosterChanged", GetRoster());
    }

    // ── Hub → Client calls (teacher dashboard calls these) ────────────────

    public async Task JoinTeacherGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "teachers");
        // Send current roster immediately
        await Clients.Caller.SendAsync("RosterChanged", GetRoster());
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_students.TryRemove(Context.ConnectionId, out _))
            await Clients.Group("teachers").SendAsync("RosterChanged", GetRoster());

        await base.OnDisconnectedAsync(exception);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<StudentStatus> GetRoster()
        => _students.Values
                    .OrderBy(s => s.JoinedAt)
                    .ToList();

    /// <summary>Called by ClassroomServerService to wipe state on stop.</summary>
    internal static void ClearAll() => _students.Clear();
}