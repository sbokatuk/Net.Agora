using Microsoft.Maui.Handlers;

#if ANDROID
using Agora.Whiteboard;
#elif IOS
using Net.Agora.Whiteboard.iOS;
#endif

namespace Net.Agora.Whiteboard.Maui;

/// <summary>
/// A MAUI view that hosts the Interactive Whiteboard — the SDK's own board view on each platform,
/// which is a WebView subclass on Android and a WKWebView subclass on iOS.
///
/// Unlike Net.Agora.Video.Maui's view, this is not a blank canvas the SDK draws into: the board
/// *is* the web view, so the handler creates the SDK's own type rather than a plain one. That is
/// also why <see cref="CreateClient" /> takes the view — the client and the board are two halves
/// of one thing, and the SDK binds them at construction.
/// </summary>
// Explicitly Microsoft.Maui.Controls.View: on Android, Android.Views.View is also in scope and
// "View" alone is ambiguous between the two.
public class AgoraWhiteboardView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// Creates a client that draws into this view, once the view has been realised — that is,
    /// after the page it is on has appeared. Calling it earlier throws, because the native board
    /// view does not exist yet.
    /// </summary>
    /// <exception cref="InvalidOperationException">The view has no handler yet.</exception>
    public AgoraWhiteboardClient CreateClient(AgoraWhiteboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (Handler?.PlatformView is not { } platformView)
        {
            throw new InvalidOperationException(
                "the whiteboard view has no native counterpart yet. Create the client after the " +
                "page has appeared rather than in its constructor — and call UseAgoraWhiteboard() " +
                "on the app builder, without which the view has no handler at all.");
        }

#if ANDROID
        return new AgoraWhiteboardClient(options, (WhiteboardView)platformView, Platform.CurrentActivity
            ?? throw new InvalidOperationException("no current activity."));
#elif IOS
        return new AgoraWhiteboardClient(options, (WhiteBoardView)platformView);
#else
        throw new PlatformNotSupportedException();
#endif
    }
}

// The handler's platform half is selected with #if rather than by putting each platform's file
// under Platforms/ — same reasoning as Net.Agora.Video.Maui: MAUI's SingleProject re-adds its own
// Platforms/<id> globs after this project's items are evaluated, which silently drops a
// hand-rolled include.

#if ANDROID

/// <summary>
/// Android half. The SDK's <c>WhiteboardView</c> is the board — a DSBridge <c>DWebView</c> — so
/// there is nothing to wrap it in.
/// </summary>
public partial class AgoraWhiteboardViewHandler : ViewHandler<AgoraWhiteboardView, WhiteboardView>
{
    /// <inheritdoc />
    protected override WhiteboardView CreatePlatformView() => new(Context);
}

#elif IOS

/// <summary>
/// iOS half. <c>WhiteBoardView</c> is a <c>WKWebView</c> subclass and is the board itself.
/// </summary>
public partial class AgoraWhiteboardViewHandler : ViewHandler<AgoraWhiteboardView, WhiteBoardView>
{
    /// <inheritdoc />
    protected override WhiteBoardView CreatePlatformView() => new();
}

#endif

/// <summary>
/// Shared half of the handler. The platform half above declares the base class, which is what
/// binds <see cref="AgoraWhiteboardView" /> to that platform's board view type.
/// </summary>
public partial class AgoraWhiteboardViewHandler
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<AgoraWhiteboardView, AgoraWhiteboardViewHandler> WhiteboardMapper =
        new PropertyMapper<AgoraWhiteboardView, AgoraWhiteboardViewHandler>(ViewHandler.ViewMapper);

    /// <summary>Creates the handler. Registered by <see cref="AppBuilderExtensions.UseAgoraWhiteboard" />.</summary>
    public AgoraWhiteboardViewHandler() : base(WhiteboardMapper)
    {
    }
}
