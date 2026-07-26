namespace Net.Agora.Video.Maui;

/// <summary>
/// Creates clients and attaches views without the caller writing platform code.
///
/// This is the whole reason the MAUI package exists. On Android the engine needs a <c>Context</c>,
/// and the native video view types differ per platform, so a MAUI app would otherwise need an
/// <c>#if ANDROID</c> block for construction and another for rendering.
/// </summary>
public static class AgoraVideoClientExtensions
{
    /// <summary>Creates a client for the current platform.</summary>
    /// <exception cref="InvalidOperationException">
    /// Android only, and only when called before the activity exists — from a page constructor,
    /// for example. Create the client after the page has appeared.
    /// </exception>
    public static AgoraVideoClient CreateClient(this AgoraVideoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

#if ANDROID
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException(
                "no current activity. RtcEngineConfig needs a Context, so create the client " +
                "after the page has appeared rather than in its constructor.");

        return new AgoraVideoClient(options, activity);
#else
        return new AgoraVideoClient(options);
#endif
    }

    /// <summary>
    /// Renders this device's own camera feed into <paramref name="view" />. Call after
    /// <see cref="IAgoraVideoClient.JoinAsync" /> and <see cref="IAgoraVideoClient.EnableVideo" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The view has no handler yet, which means it is not on screen. Attach it after the page has
    /// appeared, not from its constructor.
    /// </exception>
    public static void SetLocalView(this AgoraVideoClient client, AgoraVideoView view)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(view);

#if ANDROID
        client.SetLocalView(PlatformView(view));
#elif IOS
        client.SetLocalView(PlatformView(view));
#else
        // Unreachable while the package targets Android and iOS only, but an empty body would be
        // a silent no-op the day it is not — rendering nothing, reporting nothing.
        throw NotSupported();
#endif
    }

    /// <summary>Renders a remote user's video into <paramref name="view" /> — call after <see cref="IAgoraVideoClient.UserJoined" />.</summary>
    /// <inheritdoc cref="SetLocalView" />
    public static void SetRemoteView(this AgoraVideoClient client, uint uid, AgoraVideoView view)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(view);

#if ANDROID
        client.SetRemoteView(uid, PlatformView(view));
#elif IOS
        client.SetRemoteView(uid, PlatformView(view));
#else
        // See SetLocalView.
        throw NotSupported();
#endif
    }

#if ANDROID
    // global:: is required: this file's namespace is Net.Agora.Video.Maui, and the referenced
    // Net.Agora.Video.Android binding assembly's namespace of the same name (Net.Agora.Video.
    // Android) means an unqualified "Android.Views.SurfaceView" resolves against that sibling
    // namespace first and fails, rather than falling through to the global Android.Views one.
    private static global::Android.Views.SurfaceView PlatformView(AgoraVideoView view) =>
        view.Handler?.PlatformView as global::Android.Views.SurfaceView ?? throw NotRealised();
#elif IOS
    private static UIKit.UIView PlatformView(AgoraVideoView view) =>
        view.Handler?.PlatformView as UIKit.UIView ?? throw NotRealised();
#else
    private static object PlatformView(AgoraVideoView view) => throw NotRealised();
#endif

    private static InvalidOperationException NotRealised() =>
        new("the AgoraVideoView has no handler yet, so its native view does not exist. " +
            "Attach it once the page has appeared rather than from its constructor.");

    private static PlatformNotSupportedException NotSupported() =>
        new("Net.Agora.Video.Maui renders video on Android and iOS only.");
}
