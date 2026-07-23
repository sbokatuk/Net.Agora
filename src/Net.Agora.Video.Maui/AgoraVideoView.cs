using Microsoft.Maui.Handlers;

#if ANDROID
using Android.Views;
#elif IOS
using UIKit;
#endif

namespace Net.Agora.Video.Maui;

/// <summary>
/// A MAUI view that Agora renders video into — a plain <c>SurfaceView</c> on Android, a plain
/// <c>UIView</c> on iOS (the SDK adds its own renderer as a subview of whichever view you pass it).
///
/// A custom view rather than a wrapped <c>ContentView</c> because <c>VideoCanvas</c> /
/// <c>AgoraRtcVideoCanvas</c> want the native view itself, not a MAUI wrapper around it.
/// </summary>
// Explicitly Microsoft.Maui.Controls.View: on Android, Android.Views.View is also in scope (see
// the handler below) and "View" alone is ambiguous between the two.
public class AgoraVideoView : Microsoft.Maui.Controls.View
{
}

// The handler's platform half is selected with #if rather than by putting each platform's file
// under Platforms/ — see the equivalent comment in AntMedia.Net.Maui, the reference this package
// follows: MAUI's SingleProject re-adds its own Platforms/<id> globs after this project's items
// are evaluated, which silently drops a hand-rolled include.

#if ANDROID

/// <summary>
/// Android half: a bare <c>SurfaceView</c> is what Agora's own quickstart samples pass to
/// <c>VideoCanvas</c> — there is no SDK-provided view subclass to instantiate instead.
/// </summary>
public partial class AgoraVideoViewHandler : ViewHandler<AgoraVideoView, SurfaceView>
{
    /// <inheritdoc />
    protected override SurfaceView CreatePlatformView() => new(Context);
}

#elif IOS

/// <summary>
/// iOS half: <c>SetupLocalVideo</c>/<c>SetupRemoteVideo</c> take a plain <c>UIView</c> and the SDK
/// adds its own renderer inside it, so there is nothing SDK-specific to create here.
/// </summary>
public partial class AgoraVideoViewHandler : ViewHandler<AgoraVideoView, UIView>
{
    /// <inheritdoc />
    protected override UIView CreatePlatformView() => new() { BackgroundColor = UIColor.Black };
}

#endif

/// <summary>
/// Shared half of the handler. The platform half above declares the base class, which is what
/// binds <see cref="AgoraVideoView" /> to that platform's native view type.
/// </summary>
public partial class AgoraVideoViewHandler
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<AgoraVideoView, AgoraVideoViewHandler> VideoMapper =
        new PropertyMapper<AgoraVideoView, AgoraVideoViewHandler>(ViewHandler.ViewMapper);

    /// <summary>Creates the handler. Registered by <see cref="AppBuilderExtensions.UseAgoraVideo" />.</summary>
    public AgoraVideoViewHandler() : base(VideoMapper)
    {
    }
}
