using Microsoft.Extensions.Logging;
using SekaiToolsMauiText.Services;
using SekaiToolsMauiText.View.Translate;
using SekaiToolsMauiText.ViewModel;

namespace SekaiToolsMauiText;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<SekaiPlatformClient>();
        builder.Services.AddSingleton<PlatformSessionService>();
        builder.Services.AddSingleton<PlatformStoryService>();
        builder.Services.AddSingleton<LocalTranslationWorkspaceService>();
        builder.Services.AddSingleton<TranslatePageModel>();
        builder.Services.AddSingleton<TranslatePage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
