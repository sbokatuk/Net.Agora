namespace Net.Agora.Video.Maui;

/// <summary>Wires the package into a MAUI app.</summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Registers the handler for <see cref="AgoraVideoView" />. Without this the view has no
    /// native counterpart and renders nothing — MAUI does not discover third-party handlers on
    /// its own.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseAgoraVideo();
    /// </code>
    /// </example>
    public static MauiAppBuilder UseAgoraVideo(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<AgoraVideoView, AgoraVideoViewHandler>());

        return builder;
    }
}
