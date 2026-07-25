using Foundation;
using Net.Agora.Whiteboard.iOS;

using NativeMemberState = Net.Agora.Whiteboard.iOS.WhiteMemberState;
using NativePhase = Net.Agora.Whiteboard.iOS.WhiteRoomPhase;
using NativeRoom = Net.Agora.Whiteboard.iOS.WhiteRoom;

namespace Net.Agora.Whiteboard;

public sealed partial class AgoraWhiteboardClient
{
    private readonly WhiteSDK _sdk;
    private readonly WhiteBoardView _boardView;
    private readonly RoomCallbacks _callbacks;

    private NativeRoom? _room;

    /// <summary>
    /// Creates the SDK against a board view and joins nothing yet — call
    /// <see cref="IAgoraWhiteboardClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraWhiteboardOptions.Validate"/> for what is required.</param>
    /// <param name="boardView">
    /// The view the board renders into. It is a WKWebView subclass, so add it to a view hierarchy
    /// before joining. Use the MAUI companion package's <c>AgoraWhiteboardView</c> to have one
    /// created and wired for you.
    /// </param>
    public AgoraWhiteboardClient(AgoraWhiteboardOptions options, WhiteBoardView boardView)
    {
        ArgumentNullException.ThrowIfNull(boardView);
        options.Validate();
        _options = options;

        _boardView = boardView;
        _callbacks = new RoomCallbacks(this);

        var configuration = new WhiteSdkConfiguration(_options.AppIdentifier!)
        {
            Log = options.EnableLog,
            Region = options.Region,
        };

        _sdk = new WhiteSDK(boardView, configuration, null);
    }

    private void JoinCore(Action<AgoraWhiteboardException?> complete)
    {
        var config = new WhiteRoomConfig(_options.RoomUuid!, _options.RoomToken!, _options.Uid!)
        {
            IsWritable = _options.Writable,
            Region = _options.Region,
        };

        _sdk.JoinRoom(config, _callbacks, (_, room, error) =>
        {
            // success is passed too, but the header does not say which of the two is
            // authoritative when they disagree — so the room is what is checked.
            if (room is null)
            {
                complete(new AgoraWhiteboardException(
                    error?.LocalizedDescription ?? "the whiteboard SDK refused the join."));
                return;
            }

            _room = room;

            // The view's own Room property is what the board renders through; the SDK does not set
            // it. Miss this and the join succeeds while the view stays inert.
            _boardView.Room = room;
            complete(null);
        });
    }

    private void DisconnectCore()
    {
        _room?.Disconnect(null);
        _room = null;
        _boardView.Room = null;
    }

    private void SetToolCore(string applianceName, int[]? rgb, double? strokeWidth)
    {
        var state = new NativeMemberState { CurrentApplianceName = applianceName };

        if (rgb is not null)
        {
            state.StrokeColor = [NSNumber.FromInt32(rgb[0]), NSNumber.FromInt32(rgb[1]), NSNumber.FromInt32(rgb[2])];
        }

        if (strokeWidth is { } width)
        {
            state.StrokeWidth = NSNumber.FromDouble(width);
        }

        _room!.SetMemberState(state);
    }

    private void UndoCore() => _room!.Undo();

    private void RedoCore() => _room!.Redo();

    private void ClearCore(bool retainDocument) => _room!.CleanScene(retainDocument);

    private void SetWritableCore(bool writable, Action<AgoraWhiteboardException?> complete) =>
        _room!.SetWritable(writable, (_, error) =>
            complete(error is null
                ? null
                : new AgoraWhiteboardException(error.LocalizedDescription)));

    private void RefreshViewSizeCore() => _room?.RefreshViewSize();

    private void DisposeCore()
    {
        _room?.Disconnect(null);
        _room = null;

        // The SDK's own lifetime is the board view's — it lives on that web view's JS bridge and
        // has no release call. Disposing the view is the consumer's business, or the MAUI
        // handler's.
    }

    /// <summary>
    /// Translates <c>WhiteRoomCallbackDelegate</c>'s (all-optional) callbacks into the shared
    /// partial's Raise* calls.
    /// </summary>
    private sealed class RoomCallbacks(AgoraWhiteboardClient owner) : WhiteRoomCallbackDelegate
    {
        public override void FirePhaseChanged(NativePhase phase) =>
            owner.RaisePhaseChanged((AgoraWhiteboardPhase)(long)phase);

        public override void FireDisconnectWithError(string error) =>
            owner.RaiseDisconnected(error, kicked: false);

        public override void FireKickedWithReason(string reason) =>
            owner.RaiseDisconnected(reason, kicked: true);

        public override void FireCanUndoStepsUpdate(nint steps) => owner.RaiseUndoSteps((int)steps);

        public override void FireCanRedoStepsUpdate(nint steps) => owner.RaiseRedoSteps((int)steps);
    }
}
