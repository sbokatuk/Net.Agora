using Microsoft.Maui.Handlers;

#if ANDROID
using Agora.Fastboard;
#elif IOS
using UIKit;
#endif

namespace Net.Agora.Fastboard.Maui;

/// <summary>
/// A MAUI view that hosts Fastboard — the whiteboard with its toolbar already drawn and wired.
///
/// The two platforms disagree about who owns the view, and this is where that is hidden. On
/// Android the SDK takes a <c>FastboardView</c> the app supplies, so the handler creates one and
/// <see cref="CreateClient"/> passes it in. On iOS Fastboard creates its own view, so the client
/// is constructed first and the handler adopts the view it produced — which is why the iOS handler
/// wraps a plain container rather than an SDK type.
/// </summary>
// Explicitly Microsoft.Maui.Controls.View: on Android, Android.Views.View is also in scope and
// "View" alone is ambiguous between the two.
public class AgoraFastboardView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// Creates a client whose board is this view, once the view has been realised — that is, after
    /// the page it is on has appeared. Calling it earlier throws, because the native view does not
    /// exist yet.
    /// </summary>
    /// <exception cref="InvalidOperationException">The view has no handler yet.</exception>
    public AgoraFastboardClient CreateClient(AgoraFastboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (Handler?.PlatformView is not { } platformView)
        {
            throw new InvalidOperationException(
                "the Fastboard view has no native counterpart yet. Create the client after the " +
                "page has appeared rather than in its constructor — and call UseAgoraFastboard() " +
                "on the app builder, without which the view has no handler at all.");
        }

#if ANDROID
        return new AgoraFastboardClient(options, (FastboardView)platformView);
#elif IOS
        var client = new AgoraFastboardClient(options);

        // Fastboard built the view; this puts it inside the one MAUI laid out. Sized to fill,
        // because MAUI owns the frame and Fastboard lays its toolbar out within whatever it gets.
        var container = (UIView)platformView;
        var board = client.View;
        board.Frame = container.Bounds;
        board.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        container.AddSubview(board);

        return client;
#else
        throw new PlatformNotSupportedException();
#endif
    }
}

// The handler's platform half is selected with #if rather than by putting each platform's file
// under Platforms/ — same reasoning as the other MAUI packages here: MAUI's SingleProject re-adds
// its own Platforms/<id> globs after this project's items are evaluated, which silently drops a
// hand-rolled include.

#if ANDROID

/// <summary>
/// Android half. The SDK's <c>FastboardView</c> is the board and its toolbar, so it is created
/// here and handed to the client.
/// </summary>
public partial class AgoraFastboardViewHandler : ViewHandler<AgoraFastboardView, FastboardView>
{
    /// <inheritdoc />
    protected override FastboardView CreatePlatformView() => new(Context);
}

#elif IOS

/// <summary>
/// iOS half. A plain container, because Fastboard creates its own view and
/// <see cref="AgoraFastboardView.CreateClient" /> adds it as a subview once the client exists.
/// </summary>
public partial class AgoraFastboardViewHandler : ViewHandler<AgoraFastboardView, UIView>
{
    /// <inheritdoc />
    protected override UIView CreatePlatformView() => new();
}

#endif

/// <summary>
/// Shared half of the handler. The platform half above declares the base class, which is what
/// binds <see cref="AgoraFastboardView" /> to that platform's view type.
/// </summary>
public partial class AgoraFastboardViewHandler
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<AgoraFastboardView, AgoraFastboardViewHandler> FastboardMapper =
        new PropertyMapper<AgoraFastboardView, AgoraFastboardViewHandler>(ViewHandler.ViewMapper);

    /// <summary>Creates the handler. Registered by <see cref="AppBuilderExtensions.UseAgoraFastboard" />.</summary>
    public AgoraFastboardViewHandler() : base(FastboardMapper)
    {
    }
}
