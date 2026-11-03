using Agora.Fastboard;
using Agora.Fastboard.Extension;
using Agora.Fastboard.Model;
using Android.OS;

using NativeFastboard = Agora.Fastboard.Fastboard;
using NativeRoomPhase = Agora.Whiteboard.Domain.RoomPhase;

namespace Net.Agora.Fastboard;

public sealed partial class AgoraFastboardClient
{
    private static readonly Handler MainLooperHandler = new(Looper.MainLooper!);

    private readonly NativeFastboard _fastboard;
    private readonly RoomListener _listener;

    private FastRoom? _room;

    /// <summary>
    /// Creates the board and joins nothing yet — call <see cref="IAgoraFastboardClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraFastboardOptions.Validate"/> for what is required.</param>
    /// <param name="boardView">
    /// The view the board and its toolbar render into. Use the MAUI companion package's
    /// <c>AgoraFastboardView</c> to have one created and wired for you.
    /// </param>
    public AgoraFastboardClient(AgoraFastboardOptions options, FastboardView boardView)
    {
        ArgumentNullException.ThrowIfNull(boardView);
        options.Validate();
        _options = options;

        _fastboard = new NativeFastboard(boardView);
        _listener = new RoomListener(this);
    }

    private void JoinCore(Action<AgoraFastboardException?> complete) =>
        OnMainThread(() => JoinOnMainThread(complete), complete);

    private void JoinOnMainThread(Action<AgoraFastboardException?> complete)
    {
        // A join queued from another thread can run after Dispose, which then found no room to
        // close. Creating one now would leave it connected with nothing left to disconnect it.
        if (Volatile.Read(ref _disposed))
        {
            throw new AgoraFastboardException("the board was disposed before the join could start.");
        }

        var roomOptions = new FastRoomOptions(
            _options.AppIdentifier!,
            _options.RoomUuid!,
            _options.RoomToken!,
            _options.Uid!,
            ToRegion(_options.Region),
            _options.Writable);

        _room = _fastboard.CreateFastRoom(roomOptions)
            ?? throw new AgoraFastboardException("Fastboard.CreateFastRoom returned null.");

        _room.AddListener(_listener);

        // The room reports readiness through the listener rather than through the Join call, so
        // the completion is handed to the listener to fire — iOS is the same shape, for the same
        // reason.
        _listener.PendingJoin = complete;
        _room.Join();
    }

    // _room is read and cleared on the main thread, like the join that sets it, so a disconnect
    // queued behind a join sees the room that join created.
    private void DisconnectCore() => OnMainThread(() =>
    {
        var room = _room;
        _room = null;
        room?.Disconnect();
    });

    private void SetToolCore(AgoraFastboardTool tool, uint? color)
    {
        _room!.SetAppliance(ToAppliance(tool));

        if (color is { } rgb)
        {
            // A packed Android colour int: 0xAARRGGBB with full alpha, which is what
            // SetStrokeColor takes — unlike the whiteboard SDK's three-element array.
            _room.SetStrokeColor(unchecked((int)(0xFF000000 | rgb)));
        }
    }

    private void UndoCore() => _room!.Undo();

    private void RedoCore() => _room!.Redo();

    private void ClearCore() => _room!.CleanScene();

    private void SetWritableCore(bool writable, Action<AgoraFastboardException?> complete) =>
        _room!.SetWritable(writable, new Result(complete));

    private void DisposeCore() => DisconnectCore();

    /// <summary>
    /// Runs <paramref name="action"/> on the main thread: straight away from it, and queued there
    /// from any other, so it may then finish after this returns.
    /// </summary>
    /// <remarks>
    /// Fastboard 1.8.2 throws <c>IllegalStateException</c> ("FastRoom must be used on the main
    /// thread") from <c>Join</c>, <c>Disconnect</c> and <c>Destroy</c> anywhere else, and this
    /// façade's callers can be anywhere: an awaited call can resume on a pool thread. iOS's
    /// Fastboard has the same rule, which the shim there enforces in the same way. A queued action
    /// can no longer throw to its caller, so a failure is handed to <paramref name="failed"/>.
    /// </remarks>
    private static void OnMainThread(Action action, Action<AgoraFastboardException?>? failed = null)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return;
        }

        MainLooperHandler.Post(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception) when (failed is not null)
            {
                failed(exception as AgoraFastboardException
                    ?? new AgoraFastboardException(exception.Message, errorCode: 0, exception));
            }
        });
    }

    private static FastRegion ToRegion(string? region) => region switch
    {
        "us-sv" => FastRegion.UsSv!,
        "sg" => FastRegion.Sg!,
        "in-mum" => FastRegion.InMum!,
        "gb-lon" => FastRegion.GbLon!,
        _ => FastRegion.CnHz!,
    };

    private static FastAppliance ToAppliance(AgoraFastboardTool tool) => tool switch
    {
        AgoraFastboardTool.Eraser => FastAppliance.Eraser!,
        AgoraFastboardTool.Selector => FastAppliance.Selector!,
        AgoraFastboardTool.Text => FastAppliance.Text!,
        AgoraFastboardTool.Rectangle => FastAppliance.Rectangle!,
        AgoraFastboardTool.Ellipse => FastAppliance.Ellipse!,
        AgoraFastboardTool.Straight => FastAppliance.Straight!,
        AgoraFastboardTool.Arrow => FastAppliance.Arrow!,
        AgoraFastboardTool.Hand => FastAppliance.Hand!,
        AgoraFastboardTool.LaserPointer => FastAppliance.LaserPointer!,
        AgoraFastboardTool.Clicker => FastAppliance.Clicker!,
        _ => FastAppliance.Pencil!,
    };

    /// <summary>One native operation's completion — Fastboard's own callback shape for the few
    /// calls that have one.</summary>
    private sealed class Result(Action<AgoraFastboardException?> complete) : Java.Lang.Object, IFastResult
    {
        public void OnSuccess(Java.Lang.Object? value) => complete(null);

        public void OnError(Java.Lang.Exception? exception) =>
            complete(exception is null
                ? new AgoraFastboardException("the operation failed.")
                : new AgoraFastboardException(
                    exception.Message ?? "the operation failed.", errorCode: 0, exception));
    }

    /// <summary>
    /// Translates <c>FastRoomListener</c>'s callbacks into the shared partial's Raise* calls, and
    /// completes a pending join. The style and overlay callbacks belong to the toolbar Fastboard
    /// draws for itself and are not part of this facade.
    /// </summary>
    private sealed class RoomListener(AgoraFastboardClient owner) : Java.Lang.Object, IFastRoomListener
    {
        /// <summary>Set by JoinCore for the duration of one join.</summary>
        public Action<AgoraFastboardException?>? PendingJoin { get; set; }

        public void OnRoomReadyChanged(FastRoom? room)
        {
            // Fires with the room on success and is the only signal a join finished.
            if (room is null)
            {
                return;
            }

            var pending = PendingJoin;
            PendingJoin = null;
            pending?.Invoke(null);
        }

        public void OnFastError(FastException? error)
        {
            var message = error?.Message ?? "the board reported an error.";

            var pending = PendingJoin;
            if (pending is not null)
            {
                PendingJoin = null;
                pending(new AgoraFastboardException(message));
                return;
            }

            owner.RaiseDisconnected(message, kicked: false);
        }

        public void OnRoomPhaseChanged(NativeRoomPhase? phase)
        {
            if (phase is null)
            {
                return;
            }

            owner.RaisePhaseChanged(ToPhase(phase));
        }

        public void OnFastStyleChanged(FastStyle? style)
        {
        }

        public void OnOverlayChanged(int key)
        {
        }

        public void OnRedoUndoChanged(FastRedoUndo? count)
        {
        }

        // global:: is required: this file's namespace is Net.Agora.Fastboard, so an unqualified
        // "Agora.Whiteboard" resolves against the sibling Net.Agora.Whiteboard namespace first —
        // the same wart the Signaling client hits with Android.Runtime.
        public void OnRoomStateChanged(global::Agora.Whiteboard.Domain.RoomState? state)
        {
        }

        // A Java enum bound as a class, so compared by identity against the SDK's own singletons.
        private static AgoraFastboardPhase ToPhase(NativeRoomPhase phase)
        {
            if (phase.Equals(NativeRoomPhase.Connecting))
            {
                return AgoraFastboardPhase.Connecting;
            }

            if (phase.Equals(NativeRoomPhase.Connected))
            {
                return AgoraFastboardPhase.Connected;
            }

            if (phase.Equals(NativeRoomPhase.Reconnecting))
            {
                return AgoraFastboardPhase.Reconnecting;
            }

            return phase.Equals(NativeRoomPhase.Disconnecting)
                ? AgoraFastboardPhase.Disconnecting
                : AgoraFastboardPhase.Disconnected;
        }
    }
}
