using Agora.Whiteboard;
using Agora.Whiteboard.Domain;
using Android.Content;

using NativeMemberState = Agora.Whiteboard.Domain.MemberState;
using NativeRoom = Agora.Whiteboard.Room;
using NativeRoomPhase = Agora.Whiteboard.Domain.RoomPhase;

namespace Net.Agora.Whiteboard;

public sealed partial class AgoraWhiteboardClient
{
    private readonly WhiteSdk _sdk;
    private readonly WhiteboardView _boardView;
    private readonly RoomListener _listener;

    private NativeRoom? _room;

    /// <summary>
    /// Creates the SDK against a board view and joins nothing yet — call
    /// <see cref="IAgoraWhiteboardClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraWhiteboardOptions.Validate"/> for what is required.</param>
    /// <param name="boardView">
    /// The view the board renders into. It is a WebView subclass, so put it in a layout like any
    /// other view before joining. Use the MAUI companion package's <c>AgoraWhiteboardView</c> to
    /// have one created and wired for you.
    /// </param>
    /// <param name="context">An Android <c>Context</c> — the SDK needs one to construct.</param>
    public AgoraWhiteboardClient(AgoraWhiteboardOptions options, WhiteboardView boardView, Context context)
    {
        ArgumentNullException.ThrowIfNull(boardView);
        ArgumentNullException.ThrowIfNull(context);
        options.Validate();
        _options = options;

        _boardView = boardView;
        _listener = new RoomListener(this);

        var configuration = new WhiteSdkConfiguration(options.AppIdentifier)
        {
            EnableInterrupterAPI = false,
        };

        if (!string.IsNullOrWhiteSpace(options.Region))
        {
            // The SDK models the region as a Java enum keyed by netless's own string, so the
            // string has to be looked up rather than assigned.
            configuration.Region = Region.ValueOf(options.Region);
        }

        // The constructor is what binds the SDK to the view's JavaScript bridge; there is no
        // separate setup call, unlike iOS where the same wiring is also constructor-time.
        _sdk = new WhiteSdk(boardView, context, configuration);
    }

    private void JoinCore(Action<AgoraWhiteboardException?> complete)
    {
        var parameters = new RoomParams(_options.RoomUuid, _options.RoomToken, _options.Uid)
        {
            Writable = _options.Writable,
        };

        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            parameters.Region = Region.ValueOf(_options.Region);
        }

        _sdk.JoinRoom(parameters, _listener, new Promise<NativeRoom>(
            room =>
            {
                _room = room;
                complete(null);
            },
            error => complete(new AgoraWhiteboardException(error))));
    }

    private void DisconnectCore()
    {
        _room?.Disconnect();
        _room = null;
    }

    private void SetToolCore(string applianceName, int[]? rgb, double? strokeWidth)
    {
        var state = new NativeMemberState { CurrentApplianceName = applianceName };

        if (rgb is not null)
        {
            state.SetStrokeColor(rgb);
        }

        if (strokeWidth is { } width)
        {
            state.StrokeWidth = width;
        }

        // A property here, a method on iOS — the same JS message either way.
        _room!.MemberState = state;
    }

    private void UndoCore() => _room!.Undo();

    private void RedoCore() => _room!.Redo();

    private void ClearCore(bool retainDocument) => _room!.CleanScene(retainDocument);

    private void SetWritableCore(bool writable, Action<AgoraWhiteboardException?> complete) =>
        _room!.SetWritable(writable, new Promise<Java.Lang.Boolean>(
            _ => complete(null),
            error => complete(new AgoraWhiteboardException(error))));

    private void RefreshViewSizeCore() => _room?.RefreshViewSize();

    private void DisposeCore()
    {
        _room?.Disconnect();
        _room = null;

        // The SDK's own lifetime is the board view's: it holds the JS bridge on that WebView, and
        // there is no release call. Destroying the view is the consumer's business, or the MAUI
        // handler's.
    }

    /// <summary>
    /// One native operation's completion. The SDK answers every asynchronous call through this
    /// same <c>Promise</c> interface — <c>then</c> with the result, <c>catchEx</c> with an
    /// <c>SDKError</c> — where iOS uses a completion block per call.
    /// </summary>
    private sealed class Promise<T>(Action<T?> then, Action<string> failed) : Java.Lang.Object, IPromise
        where T : Java.Lang.Object
    {
        public void Then(Java.Lang.Object? value) => then(value as T);

        public void CatchEx(SDKError? error) =>
            failed(error?.Message ?? "the whiteboard SDK reported an error with no message.");
    }

    /// <summary>
    /// Translates <c>RoomListener</c>'s callbacks into the shared partial's Raise* calls. The
    /// room-state and append-frame callbacks are not part of this facade.
    /// </summary>
    private sealed class RoomListener(AgoraWhiteboardClient owner) : Java.Lang.Object, IRoomListener
    {
        public void OnPhaseChanged(NativeRoomPhase? phase)
        {
            if (phase is null)
            {
                return;
            }

            // A Java enum, bound as a class, so it is compared by identity against the SDK's own
            // singletons rather than switched on a value.
            owner.RaisePhaseChanged(ToPhase(phase));
        }

        public void OnDisconnectWithError(Java.Lang.Exception? error) =>
            owner.RaiseDisconnected(error?.Message ?? "the room disconnected.", kicked: false);

        public void OnKickedWithReason(string? reason) =>
            owner.RaiseDisconnected(reason ?? "removed from the room.", kicked: true);

        public void OnCanUndoStepsUpdate(long steps) => owner.RaiseUndoSteps((int)steps);

        public void OnCanRedoStepsUpdate(long steps) => owner.RaiseRedoSteps((int)steps);

        public void OnCatchErrorWhenAppendFrame(long userId, Java.Lang.Exception? error)
        {
        }

        public void OnRoomStateChanged(RoomState? state)
        {
        }

        private static AgoraWhiteboardPhase ToPhase(NativeRoomPhase phase)
        {
            if (phase.Equals(NativeRoomPhase.Connecting))
            {
                return AgoraWhiteboardPhase.Connecting;
            }

            if (phase.Equals(NativeRoomPhase.Connected))
            {
                return AgoraWhiteboardPhase.Connected;
            }

            if (phase.Equals(NativeRoomPhase.Reconnecting))
            {
                return AgoraWhiteboardPhase.Reconnecting;
            }

            return phase.Equals(NativeRoomPhase.Disconnecting)
                ? AgoraWhiteboardPhase.Disconnecting
                : AgoraWhiteboardPhase.Disconnected;
        }
    }
}
