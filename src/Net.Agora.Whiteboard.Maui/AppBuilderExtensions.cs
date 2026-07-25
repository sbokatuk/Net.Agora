namespace Net.Agora.Whiteboard.Maui;

/// <summary>Wires the package into a MAUI app.</summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Registers the handler for <see cref="AgoraWhiteboardView" />. Without this the view has no
    /// native counterpart, so it renders nothing and <c>CreateClient</c> throws — MAUI does not
    /// discover third-party handlers on its own.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseAgoraWhiteboard();
    /// </code>
    /// </example>
    public static MauiAppBuilder UseAgoraWhiteboard(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<AgoraWhiteboardView, AgoraWhiteboardViewHandler>());

        return builder;
    }
}
