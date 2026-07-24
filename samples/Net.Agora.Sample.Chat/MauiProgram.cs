using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Chat;

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
        // Nothing Agora-specific to register: Net.Agora.Chat has no handlers — chat renders
        // nothing — and Net.Agora.Chat.Maui is a plain extension method, not a MAUI service.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
