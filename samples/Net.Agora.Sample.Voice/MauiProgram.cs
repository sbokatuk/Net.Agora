using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Voice;

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
        // No handler registration: unlike Net.Agora.Video.Maui there is no view to render —
        // Net.Agora.Voice.Maui only contributes CreateClient().

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
