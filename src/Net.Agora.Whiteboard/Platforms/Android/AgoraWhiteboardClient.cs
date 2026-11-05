using Agora.Whiteboard;
using Agora.Whiteboard.Domain;
using Android.Content;
using Android.OS;
using Android.Runtime;

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
            configuration.Region = ToRegion(options.Region);
        }

        // The constructor is what binds the SDK to the view's JavaScript bridge; there is no
        // separate setup call, unlike iOS where the same wiring is also constructor-time.
        _sdk = new WhiteSdk(boardView, context, configuration);
    }

    // The SDK models the region as a Java enum, and netless's keys are only what Gson writes for its
    // constants: "us-sv" is the constant named us, so valueOf() rejects it. Anything else is looked
    // up by name, which covers the keys that are spelled like their constant ("sg", "eu").
    private static Region ToRegion(string region)
    {
        try
        {
            return region switch
            {
                "cn-hz" => Region.Cn!,
                "us-sv" => Region.Us!,
                "in-mum" => Region.InMum!,
                "gb-lon" => Region.GbLon!,
                _ => Region.ValueOf(region)!,
            };
        }
        catch (Java.Lang.IllegalArgumentException exception)
        {
            throw new AgoraWhiteboardException($"'{region}' is not a whiteboard region.", 0, exception);
        }
    }

    private void JoinCore(Action<AgoraWhiteboardException?> complete)
    {
        var parameters = new RoomParams(_options.RoomUuid, _options.RoomToken, _options.Uid)
        {
            Writable = _options.Writable,
        };

        if (!string.IsNullOrWhiteSpace(_options.Region))
        {
            parameters.Region = ToRegion(_options.Region);
        }

        // This board is in use again. The rest runs ahead of the join, on the main thread, where
        // the SDK starts its hand-over too: JoinRoom posts that to the main looper, so a post from
        // here runs first.
        BoardHandover.InUse(_boardView);

        if (Looper.MyLooper() == Looper.MainLooper)
        {
            BoardHandover.ForgetBoardsWithNothingToHandOver();
        }
        else
        {
            new Handler(Looper.MainLooper!).Post(BoardHandover.ForgetBoardsWithNothingToHandOver);
        }

        _sdk.JoinRoom(parameters, _listener, new Promise<NativeRoom>(
            room =>
            {
                // Disposed while the join was in flight. Nothing is left to disconnect this room,
                // and the hand-over guard already counts the board as let go of, so it would stay
                // in the room as this user: leave now.
                if (Volatile.Read(ref _disposed))
                {
                    room?.Disconnect();
                    complete(new AgoraWhiteboardException("the client was disposed before the join completed."));
                    return;
                }

                _room = room;
                EnableHistory(_options.Writable);
                _boardView.CallFocusView();
                _boardView.EvaluateJavascript(FixContainerHeightScript);

                complete(null);
            },
            error => complete(new AgoraWhiteboardException(error))));
    }

    // whiteboard-android's page never gives its own #root / #whiteboard-container a height: they
    // sit under a plain, unstyled div with no CSS driving it, so the canvas inside inherits 0 —
    // confirmed live over chrome://inspect (canvas w=672 h=0 with window.innerHeight correctly
    // reporting 387). A 0-height canvas neither paints nor maps touch coordinates, so drawing
    // silently does nothing and no history entry is ever recorded — no error anywhere in logcat.
    // window.innerHeight itself is unaffected and always correct, so pull the real height from
    // there rather than passing one in from the native side.
    private const string FixContainerHeightScript = """
        (function () {
            var h = window.innerHeight + 'px';
            ['root', 'whiteboard-container'].forEach(function (id) {
                var el = document.getElementById(id);
                if (el) { el.style.height = h; }
            });
            document.documentElement.style.height = h;
            document.body.style.height = h;
        })();
        """;

    private void DisconnectCore()
    {
        _room?.Disconnect();
        _room = null;
        BoardHandover.Released(_boardView);
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
            _ =>
            {
                EnableHistory(writable);
                complete(null);
            },
            error => complete(new AgoraWhiteboardException(error))));

    private void EnableHistoryCore() => _room?.DisableSerialization(false);

    private void RefreshViewSizeCore() => _room?.RefreshViewSize();

    private void DisposeCore()
    {
        _room?.Disconnect();
        _room = null;
        BoardHandover.Released(_boardView);

        // The SDK's own lifetime is the board view's: it holds the JS bridge on that WebView, and
        // there is no release call. Destroying the view is the consumer's business, or the MAUI
        // handler's, and it can happen at any time after this: the next join for the same user on
        // another board doesn't need this one to answer. See BoardHandover.
    }

    /// <summary>
    /// Keeps whiteboard-android's board-to-board hand-over from refusing a join because of a board
    /// that has nothing to hand over.
    /// </summary>
    /// <remarks>
    /// Since 2.16.134 the SDK keeps, for the whole process, which board last joined each App
    /// Identifier, room and user, in a static map on its internal <c>LifecycleHost</c>. A join for
    /// that user on another board first asks the old board's page to leave, and fails if it
    /// doesn't. Only a successful leave takes a board out of the map. <c>Room.disconnect()</c>
    /// doesn't, and neither does destroying the view. A failed leave is kept, so every later join
    /// for that user fails the same way until the process ends. Measured on an emulator:
    /// <list type="bullet">
    /// <item>A board whose view was destroyed fails with <c>StaleScope</c> straight away: its page
    /// is gone.</item>
    /// <item>A board taken off screen before its room finished disconnecting fails with
    /// <c>LeaveTimeout</c> after 20 seconds. Its page still runs, but the room stays
    /// "disconnecting".</item>
    /// </list>
    /// So before each join this forgets the boards that can't need a hand-over: those whose page is
    /// gone, and those this package has already disconnected or disposed. Either way their room has
    /// been told to leave, or no longer exists. That's how a join behaved before 2.16.134. A board
    /// whose client is still joined keeps the SDK's hand-over.
    /// <para>
    /// Neither the map nor the flag is public, so this reaches them by reflection. The native
    /// version is pinned exactly, and the device suite rejoins after each of those teardowns. If the
    /// fields aren't there, nothing is forgotten, the SDK behaves as it always would, and a warning
    /// goes to logcat under <c>Net.Agora.Whiteboard</c>. The device suite checks the fields exist.
    /// </para>
    /// </remarks>
    private static class BoardHandover
    {
        private const string LogTag = "Net.Agora.Whiteboard";

        private static readonly Fields? Native = Fields.Find();

        /// <summary>
        /// The board views this package has disconnected, until one joins again. Weak keys, so a
        /// view the app drops is still collected. Locked, because Dispose can be called from any
        /// thread.
        /// </summary>
        private static readonly Java.Util.WeakHashMap ReleasedBoards = new();

        public static void Released(WhiteboardView board)
        {
            lock (ReleasedBoards)
            {
                ReleasedBoards.Put(board, Java.Lang.Boolean.True);
            }
        }

        public static void InUse(WhiteboardView board)
        {
            lock (ReleasedBoards)
            {
                ReleasedBoards.Remove(board);
            }
        }

        private static bool WasReleased(Java.Lang.Object? board)
        {
            lock (ReleasedBoards)
            {
                return board is not null && ReleasedBoards.ContainsKey(board);
            }
        }

        /// <summary>Main thread only: the SDK reads and writes its map there, unlocked.</summary>
        public static void ForgetBoardsWithNothingToHandOver()
        {
            if (Native is not { } native)
            {
                return;
            }

            // Nothing here is disposed: Field.Get hands back the existing peer of an object .NET
            // already holds, which for the board view is the app's own.
            try
            {
                // Through the Java iterator rather than the .NET collection Values() is bound as,
                // because only the Java one can remove an entry as it goes.
                var boards = native.Owners.Get(null)?.JavaCast<Java.Util.IMap>()?.Values() as Java.Lang.Object;
                var iterator = boards?.JavaCast<Java.Lang.IIterable>().Iterator();

                while (iterator?.HasNext == true)
                {
                    var host = iterator.Next();

                    if (host is not null && (native.Invalid.GetBoolean(host) || WasReleased(native.Board.Get(host))))
                    {
                        iterator.Remove();
                    }
                }
            }
            catch (Exception exception)
            {
                // Leaves the map as it is; the join goes ahead and the SDK decides.
                global::Android.Util.Log.Warn(
                    LogTag,
                    $"couldn't clear whiteboard-android's board hand-over record, so this join may fail with StaleScope or LeaveTimeout: {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>
        /// <c>LifecycleHost</c>'s static <c>owners</c> map, its <c>invalid</c> flag, set once the
        /// page is gone, and <c>raw</c>, the board view it drives.
        /// </summary>
        private sealed record Fields(Java.Lang.Reflect.Field Owners, Java.Lang.Reflect.Field Invalid, Java.Lang.Reflect.Field Board)
        {
            public static Fields? Find()
            {
                try
                {
                    // Through the board view's own field, so the class comes from the SDK's loader.
                    var host = Java.Lang.Class.FromType(typeof(WhiteboardView)).GetDeclaredField("lifecycle").Type;
                    var fields = new Fields(
                        host.GetDeclaredField("owners"),
                        host.GetDeclaredField("invalid"),
                        host.GetDeclaredField("raw"));

                    fields.Owners.Accessible = true;
                    fields.Invalid.Accessible = true;
                    fields.Board.Accessible = true;
                    return fields;
                }
                catch (Exception exception)
                {
                    global::Android.Util.Log.Warn(
                        LogTag,
                        $"whiteboard-android's board hand-over record isn't where 2.16.134 keeps it, so a join after a destroyed or disposed board may fail with StaleScope or LeaveTimeout: {exception.GetType().Name}: {exception.Message}");
                    return null;
                }
            }
        }
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
