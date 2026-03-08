#if WINDOWS
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Nlogo.Services;

/// <summary>
/// Singleton that spins up an in-process Kestrel / ASP.NET Core server
/// hosting the SignalR ClassroomHub. Only the teacher runs this.
/// Windows only — the ASP.NET Core hosting stack is not available on iOS/Catalyst.
/// </summary>
public sealed class ClassroomServerService : IAsyncDisposable
{
    public const int HubPort = 5150;
    public const string HubPath = "/classroomhub";

    private WebApplication? _app;

    public bool IsRunning { get; private set; }
    public string? LocalIp { get; private set; }

    /// <summary>Fires whenever IsRunning or LocalIp changes.</summary>
    public event Action? StateChanged;

    // ── Start ──────────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        if (IsRunning) return;

        LocalIp = GetLocalIp();

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Listen(IPAddress.Any, HubPort);
        });

        builder.Services.AddSignalR();

        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);

        _app = builder.Build();

        _app.MapHub<ClassroomHub>(HubPath);

        await _app.StartAsync();

        IsRunning = true;
        StateChanged?.Invoke();
    }

    // ── Stop ───────────────────────────────────────────────────────────────

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            ClassroomHub.ClearAll();
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        IsRunning = false;
        StateChanged?.Invoke();
    }

    // ── Dispose ────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync() => await StopAsync();

    // ── Helpers ────────────────────────────────────────────────────────────

    public static string GetLocalIp()
    {
        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var addr in iface.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address))
                {
                    return addr.Address.ToString();
                }
            }
        }
        return "127.0.0.1";
    }

    public string HubUrl => $"http://{LocalIp}:{HubPort}{HubPath}";
}

#else

// ── Non-Windows stub ───────────────────────────────────────────────────────
// iOS and Mac Catalyst have no ASP.NET Core server runtime.
// This stub keeps DI registration and page injection working on all platforms,
// but the live classroom server feature simply does nothing when not on Windows.

namespace Nlogo.Services;

public sealed class ClassroomServerService : IAsyncDisposable
{
    public const int HubPort = 5150;
    public const string HubPath = "/classroomhub";

    public bool IsRunning => false;
    public string? LocalIp => null;
    public string HubUrl => string.Empty;

    public event Action? StateChanged;

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public static string GetLocalIp() => "127.0.0.1";
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#endif