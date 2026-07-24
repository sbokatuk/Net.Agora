using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Signaling;

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
        // Nothing Agora-specific to register: Net.Agora.Signaling has no handlers and no
        // platform glue.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
