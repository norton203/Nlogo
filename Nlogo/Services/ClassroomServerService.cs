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
            // Listen on all interfaces so students on the same WiFi can reach it
            k.Listen(IPAddress.Any, HubPort);
        });

        builder.Services.AddSignalR();

        // Suppress Kestrel/ASP.NET console noise in MAUI output window
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the first non-loopback IPv4 address (WiFi / Ethernet).</summary>
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