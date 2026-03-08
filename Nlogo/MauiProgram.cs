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
            // because ChallengeService takes it as a constructor argument.
            builder.Services.AddSingleton<ProgressPersistenceService>();
            builder.Services.AddSingleton<ChallengeService>();

            // ── Teacher portal / offline submission services ───────────────
            builder.Services.AddSingleton<StudentSettingsService>();
            builder.Services.AddSingleton<ImportService>();
            builder.Services.AddScoped<OfflineExportService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}