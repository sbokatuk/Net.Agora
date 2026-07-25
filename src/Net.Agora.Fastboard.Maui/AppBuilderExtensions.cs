namespace Net.Agora.Fastboard.Maui;

/// <summary>Wires the package into a MAUI app.</summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Registers the handler for <see cref="AgoraFastboardView" />. Without this the view has no
    /// native counterpart, so it renders nothing and <c>CreateClient</c> throws — MAUI does not
    /// discover third-party handlers on its own.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.UseMauiApp&lt;App&gt;().UseAgoraFastboard();
    /// </code>
    /// </example>
    public static MauiAppBuilder UseAgoraFastboard(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<AgoraFastboardView, AgoraFastboardViewHandler>());

        return builder;
    }
}
