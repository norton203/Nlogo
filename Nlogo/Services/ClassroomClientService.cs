using Microsoft.AspNetCore.SignalR.Client;
using Nlogo.Models;

namespace Nlogo.Services;

/// <summary>
/// Student-side SignalR client.  Call ConnectAsync() after the student
/// enters the teacher's IP. Call UpdateProgressAsync() whenever their
/// challenge progress changes.
/// </summary>
public sealed class ClassroomClientService : IAsyncDisposable
{
    private HubConnection? _hub;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public string? TeacherIp { get; private set; }

    /// <summary>Fires when connection state changes.</summary>
    public event Action? StateChanged;

    // ── Connect ────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error)> ConnectAsync(
        string teacherIp,
        string studentName)
    {
        if (IsConnected) await DisconnectAsync();

        var url = $"http://{teacherIp.Trim()}:{ClassroomServerService.HubPort}{ClassroomServerService.HubPath}";

        _hub = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _hub.Closed  += _ => { StateChanged?.Invoke(); return Task.CompletedTask; };
        _hub.Reconnected += _ => { StateChanged?.Invoke(); return Task.CompletedTask; };

        try
        {
            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            return (false, $"Could not reach teacher at {teacherIp} — {ex.Message}");
        }

        JoinResult result;
        try
        {
            result = await _hub.InvokeAsync<JoinResult>("JoinClass", studentName);
        }
        catch (Exception ex)
        {
            return (false, $"Connected but join failed — {ex.Message}");
        }

        if (!result.Success)
            return (false, result.ErrorMessage);

        TeacherIp = teacherIp;
        StateChanged?.Invoke();
        return (true, string.Empty);
    }

    // ── Push progress ──────────────────────────────────────────────────────

    public async Task UpdateProgressAsync(
        string currentChallengeId,
        string currentChallengeName,
        int totalStars,
        int completedCount)
    {
        if (!IsConnected) return;

        try
        {
            await _hub!.InvokeAsync(
                "UpdateProgress",
                currentChallengeId,
                currentChallengeName,
                totalStars,
                completedCount);
        }
        catch
        {
            // Swallow — student should never see SignalR errors in the IDE
        }
    }

    // ── Disconnect ────────────────────────────────────────────────────────

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }

        TeacherIp = null;
        StateChanged?.Invoke();
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}