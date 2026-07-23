using Microsoft.Extensions.Logging;
using Net.Agora.Video.Maui;

namespace Net.Agora.Sample;

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
            })
            // Registers the handler for AgoraVideoView. MAUI does not discover third-party
            // handlers on its own, so without this the view renders nothing.
            .UseAgoraVideo();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
