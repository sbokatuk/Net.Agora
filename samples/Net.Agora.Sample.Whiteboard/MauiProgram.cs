using Microsoft.Extensions.Logging;
using Net.Agora.Whiteboard.Maui;

namespace Net.Agora.Sample.Whiteboard;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Registers the AgoraWhiteboardView handler; without this CreateClient throws
            // because the view never gets a platform view.
            .UseAgoraWhiteboard()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
