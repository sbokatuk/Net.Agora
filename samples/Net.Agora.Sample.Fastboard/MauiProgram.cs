using Microsoft.Extensions.Logging;
using Net.Agora.Fastboard.Maui;

namespace Net.Agora.Sample.Fastboard;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // Registers the AgoraFastboardView handler; without this CreateClient throws
            // because the view never gets a platform view.
            .UseAgoraFastboard()
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
