using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Nlogo.Services;

namespace Nlogo
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            // ── Core services ──────────────────────────────────────────────
            // ProgressPersistenceService must be registered before ChallengeService
            builder.Services.AddSingleton<ProgressPersistenceService>();
            builder.Services.AddSingleton<ChallengeService>();

            // ── Teacher portal / offline submission ────────────────────────
            builder.Services.AddSingleton<StudentSettingsService>();
            builder.Services.AddSingleton<ImportService>();
            builder.Services.AddScoped<OfflineExportService>();

            // ── Live classroom ─────────────────────────────────────────────
            // Teacher side: hosts the SignalR server on the local network
            builder.Services.AddSingleton<ClassroomServerService>();
            // Student side: connects to the teacher's hub
            builder.Services.AddSingleton<ClassroomClientService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}