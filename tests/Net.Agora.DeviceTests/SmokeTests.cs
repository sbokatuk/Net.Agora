// One suite, two products: the same checks compile against the Video or the Voice façade (see
// the csproj's AgoraDeviceProduct), so the aliases below are the only per-product spelling. The
// few genuinely product-specific checks sit behind AGORA_VOICE.
#if AGORA_FASTBOARD
using Net.Agora.Fastboard;
using AgoraClient = Net.Agora.Fastboard.AgoraFastboardClient;
using AgoraClientException = Net.Agora.Fastboard.AgoraFastboardException;
using AgoraClientOptions = Net.Agora.Fastboard.AgoraFastboardOptions;
#elif AGORA_WHITEBOARD
using Net.Agora.Whiteboard;
using AgoraClient = Net.Agora.Whiteboard.AgoraWhiteboardClient;
using AgoraClientException = Net.Agora.Whiteboard.AgoraWhiteboardException;
using AgoraClientOptions = Net.Agora.Whiteboard.AgoraWhiteboardOptions;
#elif AGORA_CHAT
using Net.Agora.Chat;
using AgoraClient = Net.Agora.Chat.AgoraChatClient;
using AgoraClientException = Net.Agora.Chat.AgoraChatException;
using AgoraClientOptions = Net.Agora.Chat.AgoraChatOptions;
#elif AGORA_SIGNALING
using Net.Agora.Signaling;
using AgoraClient = Net.Agora.Signaling.AgoraSignalingClient;
using AgoraClientException = Net.Agora.Signaling.AgoraSignalingException;
using AgoraClientOptions = Net.Agora.Signaling.AgoraSignalingOptions;
#elif AGORA_VOICE
using Net.Agora.Voice;
using AgoraClient = Net.Agora.Voice.AgoraVoiceClient;
using AgoraClientException = Net.Agora.Voice.AgoraVoiceException;
using AgoraClientOptions = Net.Agora.Voice.AgoraVoiceOptions;
#else
using Net.Agora.Video;
using AgoraClient = Net.Agora.Video.AgoraVideoClient;
using AgoraClientException = Net.Agora.Video.AgoraVideoException;
using AgoraClientOptions = Net.Agora.Video.AgoraVideoOptions;
#endif

namespace Net.Agora.DeviceTests;

/// <summary>A single on-device check. Throws to fail.</summary>
/// <param name="Name">Human readable name, reported to the platform log.</param>
/// <param name="Execute">Runs the check.</param>
public sealed record SmokeTest(string Name, Func<Task> Execute)
{
    public SmokeTest(string name, Action execute)
        : this(name, () =>
        {
            execute();
            return Task.CompletedTask;
        })
    {
    }
}

/// <summary>
/// End-to-end checks that only mean anything on a real device or simulator: they load the native
/// Agora SDK out of the packaged bindings and drive it through this façade.
/// </summary>
/// <remarks>
/// <b>This file is the repository's thesis stated as a test.</b> There is one copy of it, it uses
/// nothing but the cross-platform API, and it runs unchanged on an Android emulator and an iOS
/// simulator — for the Video and the Voice façade alike. If a façade did not actually unify
/// Android's <c>RtcEngine</c> and iOS's <c>AgoraRtcEngineKit</c>, it could not compile for both
/// heads — never mind pass on both.
/// <para>
/// Unlike a Datadog-style SDK, nothing here can fully round-trip without a real, registered Agora
/// App ID: joining a channel is a live signalling call to Agora's servers, not a local no-op. So
/// these checks use a syntactically valid but unregistered App ID and assert the one thing that is
/// true either way — the server (or the timeout) rejects the join within a bounded time, and the
/// client's state machine (double-join guard, cancellation vs. timeout, dispose) behaves whether or
/// not the join itself ever succeeds. That is deliberate: it means this suite runs unconditionally,
/// with no <c>AGORA_APP_ID</c> secret required and nothing to leak if one were passed in.
/// </para>
/// <para>
/// One client for the whole suite, not one per check: both platforms' native engines are
/// process-wide singletons (<c>RtcEngine.Create</c>/<c>Destroy</c>,
/// <c>AgoraRtcEngineKit.SharedEngine</c>/<c>Destroy</c>), so repeated create/destroy churn is not
/// what a real app does and is not what is worth testing here.
/// </para>
/// <para>
/// The checks are ordered, and a failure early on cascades — which is the intent, since the first
/// failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID — so the SDK's own format
    // validation (client-side, no network needed) does not short-circuit the checks before the
    // ones that actually exercise the join path.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    private const string ChannelId = "agora-devicetests-smoke";

    private const string UserId = "net-agora-devicetests";

    /// <summary>
    /// Generous rather than tight: this runs on a shared CI runner over a real network round trip
    /// to Agora's servers, and the check only cares that a bound exists, not how tight it is.
    /// </summary>
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(25);

#if ANDROID
    /// <summary>Set by MainActivity before the checks run — Agora's engine needs a Context to construct.</summary>
    public static Android.Content.Context? AndroidContext { get; set; }
#endif

    /// <summary>Writes a line to the platform log. Set by each head.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

    /// <summary>
    /// Looks up a setting the runner script passed to this run, or null. Set by each head: the iOS
    /// head reads its environment, the Android head a file the runner script writes into the app's
    /// private storage. Only the live checks read any, and without theirs they are skipped, which is
    /// how CI runs them.
    /// </summary>
    public static Func<string, string?> Setting { get; set; } = _ => null;

    /// <summary>The client every check after construction shares. Set by <see cref="ConstructsTheClient"/>.</summary>
    private static AgoraClient? _client;

    /// <summary>Every check, in the order they must run.</summary>
    public static SmokeTest[] All =>
    [
#if AGORA_FASTBOARD
        new("a missing identifier is rejected before any native call", MissingAppIdIsRejected),
        new("constructs the board and its toolbar", ConstructsTheClient),
        new("drawing before a join is rejected by the client, not the board", DrawBeforeJoinIsRejected),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered room fails the join within the configured timeout", LoginFailsWithinTimeout),
        new("disconnect without a join is a no-op", LogoutWithoutALoginIsANoOp),
        new("disposes cleanly", DisposesCleanly),
#if ANDROID
        new("a join queued off the main thread fails if the board is disposed first", QueuedJoinAfterDisposeFails),
#endif
#elif AGORA_WHITEBOARD
        new("a missing identifier is rejected before any native call", MissingAppIdIsRejected),
        new("every documented region key reaches the SDK", AcceptsEveryRegionKey),
        new("constructs the SDK against a board view", ConstructsTheClient),
        new("drawing before a join is rejected by the client, not the board", DrawBeforeJoinIsRejected),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered room fails the join within the configured timeout", LoginFailsWithinTimeout),
        new("disconnect without a join is a no-op", LogoutWithoutALoginIsANoOp),
        new("disposes cleanly", DisposesCleanly),
#if ANDROID
        new("the SDK still keeps the board hand-over record the client clears", HandoverRecordIsWhereTheClientLooks),
#endif
        new("live: a stroke is an undo step, and Undo and Redo take it off the board and back", UndoAndRedoChangeTheScene),
        new("live: a read-only join gets its undo history once writing is granted", ReadOnlyJoinGetsHistoryWhenWritable),
#if ANDROID
        new("live: a join that completes after Dispose leaves the room", JoinThatCompletesAfterDisposeLeaves),
        new("live: the same user joins again with a new client on the same board", RejoinsOnTheSameBoard),
        new("live: the same user joins again on a new board while the last one is still joined, which takes that one out", RejoinsWhileTheLastBoardIsStillJoined),
        new("live: the same user joins again on a new board after the last one went off screen", RejoinsAfterTheLastBoardWentOffScreen),
        new("live: the same user joins again on a new board after the last one disconnected and was destroyed", RejoinsAfterTheLastBoardWasDestroyedOnceDisconnected),
        new("live: the same user joins again on a new board after the last one was destroyed at once", RejoinsAfterTheLastBoardWasDestroyed),
#endif
#elif AGORA_CHAT
        new("a missing App ID, user ID or token is rejected before any native call", MissingAppIdIsRejected),
        new("constructs the client", ConstructsTheClient),
        new("an empty renew token is rejected", RenewsAToken),
        new("sending before a sign-in fails as a chat error", SendBeforeLoginFails),
        new("the local conversation list is readable and empty", ReadsAnEmptyConversationList),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered App ID fails the sign-in within the configured timeout", LoginFailsWithinTimeout),
        new("sign-out without a sign-in is a no-op", LogoutWithoutALoginIsANoOp),
        new("disposes cleanly", DisposesCleanly),
#elif AGORA_SIGNALING
        new("a missing App ID or user ID is rejected before any native call", MissingAppIdIsRejected),
        new("constructs the client", ConstructsTheClient),
        new("an empty renew token is rejected, a shaped one is accepted", RenewsAToken),
        new("publishing before a login fails as a signaling error", PublishBeforeLoginFails),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered App ID fails the login within the configured timeout", LoginFailsWithinTimeout),
        new("logout without a login is a no-op", LogoutWithoutALoginIsANoOp),
        new("disposes cleanly", DisposesCleanly),
#else
        new("a missing App ID is rejected before any native call", MissingAppIdIsRejected),
        new("constructs a live-broadcasting broadcaster client", ConstructsTheClient),
#if AGORA_VOICE
        new("drives mute, speakerphone and volume indication without throwing", EnablesAndDisablesMedia),
#else
        new("drives the media, speakerphone and volume controls without throwing", EnablesAndDisablesMedia),
#endif
        new("an empty renew token is rejected, a shaped one is accepted", RenewsAToken),
        new("the referenced extensions' native payloads are present", ExtensionsAreAvailable),
        new("rejects a second join while one is pending", SecondJoinWhilePendingIsRejected),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered App ID fails the join within the configured timeout", JoinFailsWithinTimeout),
        new("leave after a failed join is a no-op", LeaveAfterAFailedJoinIsANoOp),
        new("disposes cleanly", DisposesCleanly),
#endif
    ];

    private static void Report(string message) => Reporter(message);

    private static AgoraClient CreateClient(AgoraClientOptions options)
    {
#if AGORA_FASTBOARD
        // Only Android hands the board a view; on iOS Fastboard creates its own, which is the one
        // platform difference the MAUI package exists to hide and the one this suite reaches
        // around, since it is not a MAUI app.
#if ANDROID
        var fastboardContext = AndroidContext ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set.");
        return new AgoraClient(options, new global::Agora.Fastboard.FastboardView(fastboardContext));
#else
        return new AgoraClient(options);
#endif
#elif AGORA_WHITEBOARD
        // The board view is the client's other half on both platforms — the SDK binds the two at
        // construction — so it is created here rather than by a Set*View call after the fact. It
        // needs no window to exist, which is what lets this suite run headless.
#if ANDROID
        var context = AndroidContext ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set.");
        return new AgoraClient(options, new global::Agora.Whiteboard.WhiteboardView(context), context);
#else
        return new AgoraClient(options, new global::Net.Agora.Whiteboard.iOS.WhiteBoardView());
#endif
#elif ANDROID && !AGORA_SIGNALING
        return new AgoraClient(
            options,
            AndroidContext ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set."));
#else
        // Signaling is the one product that needs no Context on Android — RTM has no engine to
        // hand one to — so its constructor is the same on both platforms. Chat's ChatClient.Init
        // does need one, which is why it takes the branch above.
        return new AgoraClient(options);
#endif
    }

    private static AgoraClient Client =>
        _client ?? throw new InvalidOperationException("the client has not been constructed yet.");

#if AGORA_FASTBOARD
    private static void MissingAppIdIsRejected()
    {
        // The same four identifiers the whiteboard client asks for — Fastboard is a UI layer over
        // the same service — each checked before any native call.
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions()),
            "a missing App Identifier");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId }),
            "a missing room UUID");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId, RoomUuid = ChannelId }),
            "a missing room token");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId, RoomUuid = ChannelId, RoomToken = AppId }),
            "a missing user id");
    }

    private static void ConstructsTheClient()
    {
        // On iOS this is the check with real weight: constructing reaches the Objective-C shim,
        // which reaches Fastboard, which reaches the whiteboard SDK — three Swift and Objective-C
        // layers linked out of one static library. A mangled name or a missing symbol shows up
        // here as a launch-time crash rather than a build error.
        _client = CreateClient(Options());

        Assert(!Client.IsJoined, "IsJoined is true before any join was attempted.");
    }

    private static void DrawBeforeJoinIsRejected()
    {
        Throws<AgoraClientException>(
            () => Client.SetTool(AgoraFastboardTool.Pencil, color: 0xFF0000),
            "a tool change before any join");
        Throws<AgoraClientException>(() => Client.Undo(), "an undo before any join");
    }

    private static async Task CancellationIsDistinctFromTimeout()
    {
        using var cancelSoon = new CancellationTokenSource();

        var join = Client.JoinAsync(cancelSoon.Token);
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => join,
            "a join cancelled through its own token");
    }

    private static async Task LoginFailsWithinTimeout()
    {
        var phases = new List<AgoraFastboardPhase>();
        void OnPhase(object? sender, AgoraFastboardPhaseEventArgs e)
        {
            lock (phases)
            {
                phases.Add(e.Phase);
            }
            Report($"phase: {e.Phase}");
        }

        var started = DateTimeOffset.UtcNow;

        Client.PhaseChanged += OnPhase;
        try
        {
            var error = await ThrowsAsync<AgoraClientException>(
                () => Client.JoinAsync(),
                "a join with an unregistered room");

            Report($"failed after {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0}s: {error.Message}");
        }
        finally
        {
            Client.PhaseChanged -= OnPhase;
        }

        lock (phases)
        {
            Report(phases.Count > 0
                ? $"observed {phases.Count} phase change(s)"
                : "no phase change before the rejection");
        }

        Assert(!Client.IsJoined, "IsJoined is true after a join that should have failed.");
    }

    private static void LogoutWithoutALoginIsANoOp()
    {
        Client.Disconnect();
        Client.Disconnect();

        Assert(!Client.IsJoined, "IsJoined is true after Disconnect.");
    }

    private static void DisposesCleanly()
    {
        Client.Dispose();
        // IDisposable.Dispose must tolerate being called more than once.
        Client.Dispose();

        Assert(!Client.IsJoined, "IsJoined is true after Dispose.");
    }

#if ANDROID
    private static async Task QueuedJoinAfterDisposeFails()
    {
        // Fastboard 1.8.2 takes room calls on the main thread only, so a JoinAsync from any other
        // thread is queued there. Holding the main thread while one is queued and the board is
        // disposed forces the order an app can hit by chance: the join runs after Dispose. It must
        // fail without creating a room, or that room stays connected with nothing left to close it.
        var board = CreateClient(Options());
        using var gate = new ManualResetEventSlim();
        new Android.OS.Handler(Android.OS.Looper.MainLooper!).Post(() => gate.Wait(TimeSpan.FromSeconds(10)));

        Task? join = null;
        await Task.Run(() =>
        {
            join = board.JoinAsync();
            board.Dispose();
            gate.Set();
        });

        var error = await ThrowsAsync<AgoraClientException>(() => join!, "a join queued before Dispose");
        Report(error.Message);

        Assert(error.Message.Contains("disposed", StringComparison.Ordinal),
            $"the queued join reached the board after Dispose: {error.Message}");
        Assert(!board.IsJoined, "IsJoined is true on a disposed board.");
    }
#endif

    private static AgoraClientOptions Options() => new()
    {
        AppIdentifier = AppId,
        RoomUuid = ChannelId,
        RoomToken = AppId,
        Uid = UserId,
        Timeout = JoinTimeout,
    };
#elif AGORA_WHITEBOARD
    private static void MissingAppIdIsRejected()
    {
        // The Interactive Whiteboard asks for four identifiers, from two different places: the App
        // Identifier from the Agora Console, and the room UUID, token and user id from your own
        // server's call to the whiteboard REST API. Each is checked before any native call.
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions()),
            "a missing App Identifier");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId }),
            "a missing room UUID");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId, RoomUuid = ChannelId }),
            "a missing room token");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppIdentifier = AppId, RoomUuid = ChannelId, RoomToken = AppId }),
            "a missing user id");
    }

    private static void AcceptsEveryRegionKey()
    {
        // On Android these aren't the Java enum's constant names ("us-sv" is the constant us), so
        // a plain valueOf() rejects four of the five.
        foreach (var region in new[] { "cn-hz", "us-sv", "sg", "in-mum", "gb-lon" })
        {
            var options = Options();
            options.Region = region;
            CreateClient(options).Dispose();
        }

#if ANDROID
        // iOS hands the key to the SDK as a string, so only Android can tell at construction.
        var unknown = Options();
        unknown.Region = "nowhere";
        Throws<AgoraClientException>(() => CreateClient(unknown), "an unknown region");
#endif
    }

    private static void ConstructsTheClient()
    {
        // Constructing at all is the check: it creates the SDK's own board view — a WebView on
        // Android, a WKWebView on iOS — and hands it to the SDK, which is where a binding that
        // failed to link, or a missing JavaScript bundle's package, would show up.
        _client = CreateClient(Options());

        Assert(!Client.IsJoined, "IsJoined is true before any join was attempted.");
    }

    private static void DrawBeforeJoinIsRejected()
    {
        // The facade's own guard rather than the SDK's: neither SDK has a room object to send the
        // state change to before a join, and both would throw a null reference. This is the one
        // place the facade is stricter than the thing it wraps, so it is worth pinning.
        Throws<AgoraClientException>(
            () => Client.SetTool(AgoraWhiteboardTool.Pencil, color: 0xFF0000),
            "a tool change before any join");
        Throws<AgoraClientException>(() => Client.Undo(), "an undo before any join");
    }

    private static async Task CancellationIsDistinctFromTimeout()
    {
        using var cancelSoon = new CancellationTokenSource();

        var join = Client.JoinAsync(cancelSoon.Token);

        // Cancelled synchronously, before any response could arrive, so "the caller's token wins"
        // is deterministic rather than a race against service latency.
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => join,
            "a join cancelled through its own token");
    }

    private static async Task LoginFailsWithinTimeout()
    {
        var phases = new List<AgoraWhiteboardPhase>();
        void OnPhase(object? sender, AgoraWhiteboardPhaseEventArgs e)
        {
            lock (phases)
            {
                phases.Add(e.Phase);
            }
            Report($"phase: {e.Phase}");
        }

        var started = DateTimeOffset.UtcNow;

        Client.PhaseChanged += OnPhase;
        try
        {
            var error = await ThrowsAsync<AgoraClientException>(
                () => Client.JoinAsync(),
                "a join with an unregistered room");

            Report($"failed after {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0}s: {error.Message}");
        }
        finally
        {
            Client.PhaseChanged -= OnPhase;
        }

        lock (phases)
        {
            Report(phases.Count > 0
                ? $"observed {phases.Count} phase change(s)"
                : "no phase change before the rejection");
        }

        Assert(!Client.IsJoined, "IsJoined is true after a join that should have failed.");
    }

    private static void LogoutWithoutALoginIsANoOp()
    {
        Client.Disconnect();
        Client.Disconnect();

        Assert(!Client.IsJoined, "IsJoined is true after Disconnect.");
    }

    private static void DisposesCleanly()
    {
        Client.Dispose();
        // IDisposable.Dispose must tolerate being called more than once.
        Client.Dispose();

        Assert(!Client.IsJoined, "IsJoined is true after Dispose.");
    }

    private static AgoraClientOptions Options() => new()
    {
        AppIdentifier = AppId,
        RoomUuid = ChannelId,
        RoomToken = AppId,
        Uid = UserId,
        Timeout = JoinTimeout,
    };

    // The live checks below join a real room, so they need a registered App Identifier and a room
    // token, which CI doesn't have. Without them each check is skipped, not failed.
    //
    // Each check draws in a new scene of its own, so the room's content can't hide a stroke: a
    // stroke drawn inside what's already there would leave the scene's bounds as they were. The
    // teardown takes the board back to the scene it found and removes the check's scene, whether
    // the check passed or not. The current scene is the same for everyone in a room, though, so
    // while a check runs anyone else in the room sees the empty test scene: use a room nobody else
    // is using, and run one suite at a time in it.

    /// <summary>How long the board gets to report a change. Generous for the same reason as the join.</summary>
    private static readonly TimeSpan BoardTimeout = TimeSpan.FromSeconds(15);

    private static Task UndoAndRedoChangeTheScene() => LiveBoard.RunAsync(writable: true, async live =>
    {
        // The fix under test: a room keeps no history until serialization is turned on, and turning
        // it on is what makes the board report both counts at once. Before it, nothing was raised
        // here at all, however much was drawn.
        await live.History.WaitForAsync(h => h == (0, 0), "a writable join to report an empty history");

        var empty = await live.MoveToNewSceneAsync();
        await live.DrawStrokeAsync();

        var drawn = await live.WaitForSceneAsync(scene => scene != empty, "the stroke to reach the scene");
        await live.History.WaitForAsync(h => h == (1, 0), "the stroke to be one undo step");

        live.Client.Undo();
        await live.History.WaitForAsync(h => h == (0, 1), "Undo to make the stroke a redo step");
        await live.WaitForSceneAsync(scene => scene == empty, "Undo to take the stroke off the scene");

        live.Client.Redo();
        await live.History.WaitForAsync(h => h == (1, 0), "Redo to make the stroke an undo step again");
        await live.WaitForSceneAsync(scene => scene == drawn, "Redo to put the same stroke back");
    });

    private static Task ReadOnlyJoinGetsHistoryWhenWritable() => LiveBoard.RunAsync(writable: false, async live =>
    {
        // A read-only room refuses to turn serialization on, so the client doesn't ask: nothing is
        // reported, and Undo is a harmless no-op rather than an error.
        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert(live.History.Count == 0, $"a read-only join reported history {live.History.Latest}.");
        live.Client.Undo();

        await live.Client.SetWritableAsync(true);
        await live.History.WaitForAsync(h => h == (0, 0), "granting writing to report an empty history");

        var empty = await live.MoveToNewSceneAsync();
        await live.DrawStrokeAsync();
        await live.WaitForSceneAsync(scene => scene != empty, "the stroke to reach the scene");
        await live.History.WaitForAsync(h => h == (1, 0), "the stroke to be one undo step");

        live.Client.Undo();
        await live.WaitForSceneAsync(scene => scene == empty, "Undo to take the stroke off the scene");
    });

#if ANDROID
    // whiteboard-android 2.16.134 remembers, for the whole process, which board last joined each App
    // Identifier, room and user. A join for that user on another board first asks the old board to
    // leave. Before 2.16.147.3, a board that couldn't answer made that join fail, and every later one
    // for the same user: a destroyed view with StaleScope, and a view taken off screen while its room
    // was still disconnecting with LeaveTimeout. Each check below is one way an app ends up with a
    // second board for the same user, and each has a user id of its own, so a failure in one can't
    // spread to the next.

    /// <summary>
    /// The client clears whiteboard-android's private record of which board holds each user, by
    /// reflection, and quietly does nothing if it isn't there. This needs no room, so CI runs it:
    /// a native pin whose SDK moved the record fails here rather than shipping the rejoin failures
    /// back. The names are the ones <c>BoardHandover</c> reads.
    /// </summary>
    private static void HandoverRecordIsWhereTheClientLooks()
    {
        var lifecycle = Java.Lang.Class.FromType(typeof(global::Agora.Whiteboard.WhiteboardView)).GetDeclaredField("lifecycle");
        var host = lifecycle.Type;
        Assert(host.Name == "com.herewhite.sdk.LifecycleHost", $"WhiteboardView.lifecycle is a {host.Name}.");

        var owners = host.GetDeclaredField("owners");
        Assert(
            Java.Lang.Reflect.Modifier.IsStatic(owners.Modifiers) && owners.Type.Name == "java.util.Map",
            $"LifecycleHost.owners is {Java.Lang.Reflect.Modifier.ToString(owners.Modifiers)} {owners.Type.Name}, not a static java.util.Map.");

        var invalid = host.GetDeclaredField("invalid");
        Assert(invalid.Type.Name == "boolean", $"LifecycleHost.invalid is a {invalid.Type.Name}, not a boolean.");

        var raw = host.GetDeclaredField("raw");
        Assert(
            raw.Type.IsAssignableFrom(Java.Lang.Class.FromType(typeof(global::Agora.Whiteboard.WhiteboardView))),
            $"LifecycleHost.raw is a {raw.Type.Name}, which can't hold the board view.");
    }

    /// <summary>
    /// A join still in flight when its client is disposed. Disposing counts the board as let go of,
    /// so a later join for the same user skips the SDK's hand-over: the room this join makes has to
    /// leave by itself, or it stays in the room as that user.
    /// </summary>
    private static async Task JoinThatCompletesAfterDisposeLeaves()
    {
        var options = LiveBoard.Options($"{UserId}-late-join", writable: true);
        var activity = (Android.App.Activity)(AndroidContext
            ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set."));
        var board = new global::Agora.Whiteboard.WhiteboardView(activity);
        activity.SetContentView(board);
        var client = new AgoraClient(options, board, activity);

        try
        {
            var join = client.JoinAsync();
            client.Dispose();

            AgoraClientException? error = null;
            try
            {
                await join;
            }
            catch (AgoraClientException exception)
            {
                error = exception;
                Report($"the join failed: {exception.Message}");
            }

            var deadline = DateTime.UtcNow + BoardTimeout;
            string phase;
            while ((phase = (await RoomPhaseAsync(board)).Trim('"')) is not ("disconnected" or "no room object in the page"))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException($"the room the late join made is still {phase}.");
                }

                await Task.Delay(200);
            }

            Report($"the late join's room: {phase}");
            Assert(error is not null, "a join that completed after Dispose was reported as joined.");
        }
        finally
        {
            client.Dispose();
            activity.SetContentView(new Android.Views.View(activity));
            board.Destroy();
        }
    }

    private static Task RejoinsOnTheSameBoard() => RejoinAsync("same-board", FirstBoard.Reused);

    private static Task RejoinsWhileTheLastBoardIsStillJoined() => RejoinAsync("still-joined", FirstBoard.StillJoined);

    private static Task RejoinsAfterTheLastBoardWentOffScreen() => RejoinAsync("off-screen", FirstBoard.OffScreen);

    private static Task RejoinsAfterTheLastBoardWasDestroyedOnceDisconnected() =>
        RejoinAsync("destroyed-later", FirstBoard.DestroyedOnceDisconnected);

    private static Task RejoinsAfterTheLastBoardWasDestroyed() => RejoinAsync("destroyed", FirstBoard.Destroyed);

    /// <summary>What happens to the first board before the same user joins again.</summary>
    private enum FirstBoard
    {
        /// <summary>Its client disposed, and the second client made on the same view.</summary>
        Reused,

        /// <summary>
        /// Its client still joined, on screen next to the second board. The client hasn't let go, so
        /// the SDK's own hand-over takes it out of the room.
        /// </summary>
        StillJoined,

        /// <summary>
        /// Its client disposed and the view taken straight off screen, then left alone, as when an app
        /// swaps the board out of its layout.
        /// </summary>
        OffScreen,

        /// <summary>Disconnected, and its view destroyed once the room reported it.</summary>
        DestroyedOnceDisconnected,

        /// <summary>Its view destroyed straight after Dispose, as an activity's OnDestroy would.</summary>
        Destroyed,
    }

    private static async Task RejoinAsync(string user, FirstBoard first)
    {
        var options = LiveBoard.Options($"{UserId}-rejoin-{user}", writable: true);
        var activity = (Android.App.Activity)(AndroidContext
            ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set."));
        var views = new List<global::Agora.Whiteboard.WhiteboardView>();
        var destroyed = new HashSet<global::Agora.Whiteboard.WhiteboardView>();

        global::Agora.Whiteboard.WhiteboardView NewBoard()
        {
            var board = new global::Agora.Whiteboard.WhiteboardView(activity);
            views.Add(board);
            return board;
        }

        var view = NewBoard();
        activity.SetContentView(view);
        var client = new AgoraClient(options, view, activity);

        try
        {
            await client.JoinAsync();
            Report($"joined on the first board as {options.Uid}");

            if (first == FirstBoard.DestroyedOnceDisconnected)
            {
                await DisconnectAndWaitAsync(client);
            }

            var old = view;
            var oldClient = client;
            var handedOver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            if (first == FirstBoard.StillJoined)
            {
                client.PhaseChanged += (_, e) =>
                {
                    if (e.Phase == AgoraWhiteboardPhase.Disconnected)
                    {
                        handedOver.TrySetResult();
                    }
                };
            }
            else
            {
                client.Dispose();
            }

            switch (first)
            {
                case FirstBoard.StillJoined:
                    var layout = new Android.Widget.LinearLayout(activity) { Orientation = Android.Widget.Orientation.Vertical };
                    activity.SetContentView(layout);
                    layout.AddView(old, new Android.Widget.LinearLayout.LayoutParams(-1, 0, 1));
                    view = NewBoard();
                    layout.AddView(view, new Android.Widget.LinearLayout.LayoutParams(-1, 0, 1));
                    break;

                case FirstBoard.OffScreen:
                    view = NewBoard();
                    activity.SetContentView(view);
                    Report($"the first board's room, off screen: {await RoomPhaseAsync(old)}");
                    break;

                case FirstBoard.Destroyed or FirstBoard.DestroyedOnceDisconnected:
                    view = NewBoard();
                    activity.SetContentView(view);
                    old.Destroy();
                    destroyed.Add(old);
                    break;
            }

            client = new AgoraClient(options, view, activity);
            var started = DateTime.UtcNow;

            try
            {
                await client.JoinAsync();
                Report($"joined again after {(DateTime.UtcNow - started).TotalSeconds:0.0}s");

                if (first == FirstBoard.StillJoined)
                {
                    Assert(
                        await Task.WhenAny(handedOver.Task, Task.Delay(BoardTimeout)) == handedOver.Task,
                        "the first client, still joined, was never taken out of the room.");
                    Report("the first client was taken out of the room");
                }
            }
            finally
            {
                oldClient.Dispose();
            }
        }
        finally
        {
            client.Dispose();
            activity.SetContentView(new Android.Views.View(activity));

            foreach (var board in views.Where(board => !destroyed.Contains(board)))
            {
                board.Destroy();
            }
        }
    }

    /// <summary>The phase of the room in a board's page, as the page itself reports it.</summary>
    private static async Task<string> RoomPhaseAsync(global::Agora.Whiteboard.WhiteboardView board)
    {
        var phase = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        board.EvaluateJavascript(
            "(function () { return window.room ? String(window.room.phase) : 'no room object in the page'; })()",
            new JavascriptResult(json => phase.TrySetResult(json)));

        return await Task.WhenAny(phase.Task, Task.Delay(BoardTimeout)) == phase.Task
            ? phase.Task.Result ?? "nothing"
            : "no answer from the page";
    }

    private static async Task DisconnectAndWaitAsync(AgoraClient client)
    {
        var left = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPhase(object? sender, AgoraWhiteboardPhaseEventArgs e)
        {
            if (e.Phase == AgoraWhiteboardPhase.Disconnected)
            {
                left.TrySetResult();
            }
        }

        client.PhaseChanged += OnPhase;
        var started = DateTime.UtcNow;
        client.Disconnect();
        var disconnected = await Task.WhenAny(left.Task, Task.Delay(BoardTimeout)) == left.Task;
        client.PhaseChanged -= OnPhase;

        if (!disconnected)
        {
            throw new InvalidOperationException("the room never reported the disconnect.");
        }

        Report($"disconnected after {(DateTime.UtcNow - started).TotalSeconds:0.0}s");
    }
#endif

    /// <summary>
    /// A client joined to the live room, with its board view on screen. The board is a web page, so
    /// a stroke is drawn as touch events dispatched in that page and the scene is read from the
    /// page's own room object: neither needs touch injection, which the iOS simulator can't do from
    /// a test, and both behave the same on the two platforms.
    /// </summary>
    private sealed class LiveBoard
    {
        /// <summary>Where every check's scene goes; each check adds a directory of its own under it.</summary>
        private const string TestScenes = "/net-agora-device-tests";

        /// <summary>The check's own scene directory, once one has been made. The teardown removes it.</summary>
        private string? _sceneDirectory;

#if ANDROID
        private readonly global::Agora.Whiteboard.WhiteboardView _view;
#else
        private readonly global::Net.Agora.Whiteboard.iOS.WhiteBoardView _view;
#endif

        private LiveBoard(
            AgoraClient client,
#if ANDROID
            global::Agora.Whiteboard.WhiteboardView view)
#else
            global::Net.Agora.Whiteboard.iOS.WhiteBoardView view)
#endif
        {
            Client = client;
            _view = view;
            History = new HistoryLog(client);
        }

        public AgoraClient Client { get; }

        public HistoryLog History { get; }

        /// <summary>
        /// Joins the live room, runs <paramref name="check"/> and tears down, whatever the check did.
        /// When the check fails, its failure is the one reported and a teardown failure on top of it
        /// is only logged. When the check passes, a teardown that couldn't clean up fails it.
        /// </summary>
        public static async Task RunAsync(bool writable, Func<LiveBoard, Task> check)
        {
            var live = Create(writable);
            Exception? failure = null;

            try
            {
                await live.JoinAsync(writable);
                await check(live);
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                try
                {
                    await live.TearDownAsync();
                }
                catch (Exception teardown) when (failure is not null)
                {
                    Report($"the teardown failed as well: {teardown.GetType().Name}: {teardown.Message}");
                }
            }
        }

        private static LiveBoard Create(bool writable)
        {
            // A user id per check. On Android, whiteboard 2.16.134 hands a session for the same App
            // Identifier, room and user from one board to the next, so each check gets its own, and
            // only the rejoin checks above test that hand-over.
            var options = Options($"{UserId}-{(writable ? "writer" : "reader")}", writable);

#if ANDROID
            var context = AndroidContext ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set.");
            var view = new global::Agora.Whiteboard.WhiteboardView(context);
            var client = new AgoraClient(options, view, context);

            // On screen, so the page lays out at the activity's size as it would in an app.
            ((Android.App.Activity)context).SetContentView(view);
#else
            var view = new global::Net.Agora.Whiteboard.iOS.WhiteBoardView();
            var client = new AgoraClient(options, view);
            var root = (UIKit.UIApplication.SharedApplication.Delegate as AppDelegate)?.Window?.RootViewController?.View
                ?? throw new InvalidOperationException("the app has no window to show the board in.");
            view.Frame = root.Bounds;
            root.AddSubview(view);
#endif

            return new LiveBoard(client, view);
        }

        /// <summary>The live room's options for <paramref name="uid"/>, or a skip when this run names no room.</summary>
        public static AgoraClientOptions Options(string uid, bool writable)
        {
            var appIdentifier = Setting("AGORA_WHITEBOARD_APP_IDENTIFIER");
            var roomUuid = Setting("AGORA_WHITEBOARD_ROOM_UUID");
            var roomToken = Setting("AGORA_WHITEBOARD_ROOM_TOKEN");

            if (string.IsNullOrEmpty(appIdentifier) || string.IsNullOrEmpty(roomUuid) || string.IsNullOrEmpty(roomToken))
            {
                throw new SmokeTestSkippedException(
                    "no live room. Give the runner script AGORA_WHITEBOARD_APP_IDENTIFIER, AGORA_WHITEBOARD_ROOM_UUID, " +
                    "AGORA_WHITEBOARD_ROOM_TOKEN and, for a room outside China, AGORA_WHITEBOARD_REGION.");
            }

            return new AgoraClientOptions
            {
                AppIdentifier = appIdentifier,
                RoomUuid = roomUuid,
                RoomToken = roomToken,
                Region = Setting("AGORA_WHITEBOARD_REGION") is { Length: > 0 } region ? region : null,
                Uid = uid,
                Writable = writable,
                Timeout = JoinTimeout,
            };
        }

        private async Task JoinAsync(bool writable)
        {
            await Client.JoinAsync();

            // Pencil, explicitly, so the stroke doesn't depend on the room's last tool.
            if (writable)
            {
                Client.SetTool(AgoraWhiteboardTool.Pencil, color: 0x1E88E5, strokeWidth: 8);
            }
        }

        /// <summary>
        /// Makes a new scene for this check and moves the board to it, so a stroke can't be lost in
        /// what the room already holds. Needs writing. Returns the new scene's bounds, which are
        /// empty.
        /// </summary>
        public async Task<string> MoveToNewSceneAsync()
        {
            // Set first, so the teardown removes the directory even if the move fails halfway.
            var directory = _sceneDirectory = $"{TestScenes}/{Guid.NewGuid():N}";
            var scene = $"{directory}/board";

            // The scene to go back to is kept in the page, so no path has to be quoted into a script.
            var moved = await EvaluateAsync(InRoom(
                "window.__netAgoraReturnPath = room.state.sceneState.scenePath; " +
                "var bounds = JSON.stringify(room.getBoundingRect(window.__netAgoraReturnPath)); " +
                $"room.putScenes('{directory}', [{{ name: 'board' }}]); " +
                $"room.setScenePath('{scene}'); " +
                "return 'left ' + window.__netAgoraReturnPath + ' with bounds ' + bounds;"));

            if (moved is null || !moved.StartsWith("left ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"couldn't make a scene for the check: {moved ?? "the page returned nothing"}.");
            }

            Report(moved);
            await WaitForPageAsync(InRoom("return room.state.sceneState.scenePath;"), scene, $"the board to move to {scene}");

            var empty = await SceneAsync();
            Assert(
                await EvaluateAsync(InRoom("var r = room.getBoundingRect(room.state.sceneState.scenePath); return String(r.width === 0 && r.height === 0);")) == "true",
                $"the new scene {scene} isn't empty: its bounds are {empty}.");
            Report($"drawing in {scene}, bounds {empty}");
            return empty;
        }

        /// <summary>
        /// The bounding box of everything on the current scene, as the page's room reports it. In
        /// an empty scene, any stroke changes it and taking the stroke away puts it back.
        /// </summary>
        public async Task<string> SceneAsync()
        {
            var scene = await EvaluateAsync(InRoom("return JSON.stringify(room.getBoundingRect(room.state.sceneState.scenePath));"));

            if (scene is null || !scene.StartsWith('{'))
            {
                throw new InvalidOperationException($"couldn't read the scene: {scene ?? "the page returned nothing"}.");
            }

            return scene;
        }

        public async Task<string> WaitForSceneAsync(Func<string, bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + BoardTimeout;
            var scene = await SceneAsync();

            while (!condition(scene))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException($"timed out waiting for {what}: the scene's bounds stayed {scene}.");
                }

                await Task.Delay(200);
                scene = await SceneAsync();
            }

            Report($"scene: {scene}");
            return scene;
        }

        /// <summary>Polls a script in the board page until it returns <paramref name="expected"/>.</summary>
        private async Task WaitForPageAsync(string script, string expected, string what)
        {
            var deadline = DateTime.UtcNow + BoardTimeout;
            string? value;

            while ((value = await EvaluateAsync(script)) != expected)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException($"timed out waiting for {what}: the page said {value ?? "nothing"}.");
                }

                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Wraps <paramref name="body"/> so it runs against the page's room and returns a string
        /// either way: a script that throws comes back as its error rather than as nothing.
        /// </summary>
        private static string InRoom(string body) =>
            "(function () { try { var room = window.room; if (!room) { return 'no room object in the page'; } " +
            body +
            " } catch (e) { return 'the page threw: ' + e; } })()";

        /// <summary>
        /// Draws one diagonal stroke across the middle of the board: a finger's worth of touch
        /// events, about 60 a second. On a phone the board listens only for touch events and reads
        /// only each touch's identifier and position, so these are plain <c>Event</c>s carrying a
        /// touch list. WebKit on iOS can't construct a <c>Touch</c>, so a real <c>TouchEvent</c>
        /// couldn't be built the same way on both platforms.
        /// </summary>
        public async Task DrawStrokeAsync()
        {
            var started = await EvaluateAsync(StrokeScript);
            Report(started ?? "the stroke script returned nothing");

            var deadline = DateTime.UtcNow + BoardTimeout;
            while (await EvaluateAsync("String(window.__netAgoraStroke)") != "done")
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException($"the stroke never finished: {started}.");
                }

                await Task.Delay(50);
            }
        }

        private const string StrokeScript = """
            (function () {
                var w = window.innerWidth, h = window.innerHeight;
                var target = document.elementFromPoint(w / 2, h / 2);
                if (!target) { return 'nothing at the middle of a ' + w + 'x' + h + ' page'; }
                function fire(type, x, y) {
                    var touch = { identifier: 7, target: target, clientX: x, clientY: y, pageX: x, pageY: y, screenX: x, screenY: y, force: 1 };
                    var one = { length: 1, 0: touch, item: function (i) { return i === 0 ? touch : null; } };
                    var none = { length: 0, item: function () { return null; } };
                    var event = new Event(type, { bubbles: true, cancelable: true });
                    Object.defineProperty(event, 'changedTouches', { value: one });
                    Object.defineProperty(event, 'touches', { value: type === 'touchend' ? none : one });
                    Object.defineProperty(event, 'targetTouches', { value: type === 'touchend' ? none : one });
                    target.dispatchEvent(event);
                }
                var x0 = w * 0.3, y0 = h * 0.35, x1 = w * 0.7, y1 = h * 0.65, steps = 15, step = 0;
                window.__netAgoraStroke = 'drawing';
                fire('touchstart', x0, y0);
                (function next() {
                    step++;
                    var x = x0 + (x1 - x0) * step / steps, y = y0 + (y1 - y0) * step / steps;
                    fire('touchmove', x, y);
                    if (step < steps) { setTimeout(next, 16); return; }
                    fire('touchend', x, y);
                    window.__netAgoraStroke = 'done';
                })();
                return 'stroke on ' + target.tagName.toLowerCase() + ' in a ' + w + 'x' + h + ' page';
            })()
            """;

        /// <summary>
        /// Runs a script in the board page and returns its value as a string. Only a string comes
        /// back the same way on both platforms, so every script here returns one.
        /// </summary>
        private Task<string?> EvaluateAsync(string script)
        {
            var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
#if ANDROID
            // WebView hands the value back encoded as JSON, so a string arrives quoted.
            _view.EvaluateJavascript(script, new JavascriptResult(json =>
                result.TrySetResult(json is null or "null"
                    ? null
                    : new Org.Json.JSONTokener(json).NextValue()?.ToString())));
#else
            _view.EvaluateJavaScript(script, (value, error) =>
            {
                if (error is not null)
                {
                    result.TrySetException(new InvalidOperationException($"the board page threw: {error.LocalizedDescription}"));
                }
                else
                {
                    result.TrySetResult(value?.ToString());
                }
            });
#endif
            return result.Task;
        }

        /// <summary>
        /// Removes the check's scene, leaves the room and takes the board off screen. It always
        /// leaves and always takes the board off, even when the scene couldn't be removed; that
        /// failure is thrown at the end.
        /// </summary>
        private async Task TearDownAsync()
        {
            Exception? sceneLeftBehind = null;

            if (_sceneDirectory is { } directory)
            {
                try
                {
                    await RemoveSceneAsync(directory);
                }
                catch (Exception exception)
                {
                    sceneLeftBehind = new InvalidOperationException(
                        $"the check's scene {directory} may still be in the room: {exception.Message}", exception);
                }
            }

            await LeaveAsync();

            if (sceneLeftBehind is not null)
            {
                throw sceneLeftBehind;
            }
        }

        /// <summary>
        /// Takes the board back to the scene it was on before the check and removes the check's
        /// scene directory, with whatever was drawn in it. Doesn't depend on Undo working.
        /// </summary>
        private async Task RemoveSceneAsync(string directory)
        {
            if (!Client.IsJoined)
            {
                throw new InvalidOperationException("the client had already left the room.");
            }

            var removed = await EvaluateAsync(InRoom(
                "var back = window.__netAgoraReturnPath; " +
                $"if (back && room.state.sceneState.scenePath.indexOf('{directory}/') === 0) {{ room.setScenePath(back); }} " +
                $"room.removeScenes('{directory}'); " +
                "return 'removed';"));

            if (removed != "removed")
            {
                throw new InvalidOperationException(removed ?? "the page returned nothing.");
            }

            await WaitForPageAsync(InRoom($"return room.scenePathType('{directory}');"), "none", $"{directory} to be removed");
            Report(await EvaluateAsync(InRoom(
                $"return 'removed {directory}; back on ' + room.state.sceneState.scenePath + ' with bounds ' + " +
                "JSON.stringify(room.getBoundingRect(room.state.sceneState.scenePath));")) ?? $"removed {directory}");
        }

        private async Task LeaveAsync()
        {
            // Lets the room finish leaving before its page goes, so the check's last phase changes
            // reach the log. A later join for the same user doesn't depend on it: see the rejoin
            // checks.
            if (Client.IsJoined)
            {
                var left = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnPhase(object? sender, AgoraWhiteboardPhaseEventArgs e)
                {
                    if (e.Phase == AgoraWhiteboardPhase.Disconnected)
                    {
                        left.TrySetResult();
                    }
                }

                Client.PhaseChanged += OnPhase;
                var started = DateTime.UtcNow;
                Client.Disconnect();
                var disconnected = await Task.WhenAny(left.Task, Task.Delay(BoardTimeout)) == left.Task;
                Client.PhaseChanged -= OnPhase;
                Report(disconnected
                    ? $"disconnected after {(DateTime.UtcNow - started).TotalSeconds:0.0}s"
                    : "the room never reported the disconnect");
            }

            History.Dispose();
            Client.Dispose();
#if ANDROID
            ((Android.App.Activity)_view.Context!).SetContentView(new Android.Views.View(_view.Context));
            _view.Destroy();
#else
            _view.RemoveFromSuperview();
#endif
        }
    }

#if ANDROID
    private sealed class JavascriptResult(Action<string?> received) : Java.Lang.Object, Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => received(value?.ToString());
    }
#endif

    /// <summary>Keeps the latest <c>HistoryChanged</c> report as an (undo, redo) pair.</summary>
    private sealed class HistoryLog : IDisposable
    {
        private readonly AgoraClient _client;
        private readonly object _gate = new();
        private (int Undo, int Redo)? _latest;
        private int _count;

        public HistoryLog(AgoraClient client)
        {
            _client = client;
            _client.HistoryChanged += OnHistoryChanged;
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _count;
                }
            }
        }

        public (int Undo, int Redo)? Latest
        {
            get
            {
                lock (_gate)
                {
                    return _latest;
                }
            }
        }

        /// <summary>
        /// Waits for the latest report to satisfy <paramref name="condition"/>. The two counts
        /// arrive on separate callbacks, so the pair can pass through a state that is neither the
        /// old one nor the new one. Only where it settles is checked.
        /// </summary>
        public async Task WaitForAsync(Func<(int Undo, int Redo), bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + BoardTimeout;

            while (Latest is not { } latest || !condition(latest))
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new InvalidOperationException(Latest is { } last
                        ? $"timed out waiting for {what}: the history stayed at undo={last.Undo} redo={last.Redo}."
                        : $"timed out waiting for {what}: HistoryChanged was never raised.");
                }

                await Task.Delay(100);
            }

            var settled = Latest!.Value;
            Report($"history: undo={settled.Undo} redo={settled.Redo}");
        }

        public void Dispose() => _client.HistoryChanged -= OnHistoryChanged;

        private void OnHistoryChanged(object? sender, AgoraWhiteboardHistoryEventArgs e)
        {
            lock (_gate)
            {
                _latest = (e.UndoSteps, e.RedoSteps);
                _count++;
            }
        }
    }
#elif AGORA_CHAT
    private static void MissingAppIdIsRejected()
    {
        // The one call in this façade's own code (rather than the bindings') that validates before
        // touching a native client. Chat asks for the most of any product here: an App ID (or the
        // Easemob-style app key, but not both), a user ID, and a token — Chat has no App ID-only
        // authentication mode to fall back on.
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions()),
            "a missing App ID");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppId = AppId, AppKey = "org#app" }),
            "both an App ID and an app key");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppId = AppId }),
            "a missing user ID");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppId = AppId, UserId = UserId }),
            "a missing token");
    }

    private static void ConstructsTheClient()
    {
        _client = CreateClient(new AgoraClientOptions
        {
            AppId = AppId,
            UserId = UserId,
            Token = AppId,
            Timeout = JoinTimeout,
        });

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true before any sign-in was attempted.");
        Assert(Client.CurrentUserId is null, "CurrentUserId is set before any sign-in was attempted.");
    }

    private static void RenewsAToken()
    {
        // Only the façade's own guard is exercised. Unlike Signaling's RenewToken — a fire-and-
        // forget call — Chat's is awaitable, and neither SDK promises a callback for a renewal
        // outside a session, so a shaped token here would hang until the timeout rather than
        // prove anything.
        ThrowsAsync<ArgumentException>(() => Client.RenewTokenAsync(" "), "a whitespace renew token")
            .GetAwaiter().GetResult();
    }

    private static async Task SendBeforeLoginFails()
    {
        // The cheapest full round trip the suite has: the call crosses the bridge, the SDK
        // rejects it (not signed in), and the failure comes back as the façade's typed exception —
        // no network, no credentials. It is also the one check that proves the two very different
        // completion paths work, since Android reports a send through a callback attached to the
        // message and iOS through a block on the send call.
        var error = await ThrowsAsync<AgoraClientException>(
            () => Client.SendTextMessageAsync("net-agora-devicetests-peer", "hello"),
            "a send before any sign-in");

        Report($"send rejected: [{error.ErrorCode}] {error.Message}");
    }

    private static void ReadsAnEmptyConversationList()
    {
        // Local-only, so it answers without a session — and on a fresh install there is nothing in
        // the SDK's database to answer with. What is being proved is that the two very different
        // native shapes (Android's sorted List, iOS's unordered array) both come back as an
        // ordinary empty IReadOnlyList rather than null or a throw.
        var conversations = Client.GetConversations();

        Assert(conversations is not null, "GetConversations returned null.");
        Assert(conversations!.Count == 0, $"GetConversations returned {conversations.Count} on a fresh install.");
    }

    private static async Task CancellationIsDistinctFromTimeout()
    {
        using var cancelSoon = new CancellationTokenSource();

        var login = Client.LoginAsync(cancelSoon.Token);

        // Cancelled synchronously, before any response could arrive, so "the caller's token wins"
        // is deterministic rather than a race against service latency — same reasoning as the
        // other flavours.
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => login,
            "a sign-in cancelled through its own token");
    }

    private static async Task LoginFailsWithinTimeout()
    {
        // A failing sign-in is also the one moment this credential-less suite can see the
        // connection lifecycle move, so the event wiring is asserted here rather than in a check
        // of its own.
        var states = new List<AgoraChatConnectionState>();
        void OnState(object? sender, AgoraChatConnectionStateEventArgs e)
        {
            lock (states)
            {
                states.Add(e.State);
            }
            Report($"connection state: {e.State}");
        }

        var started = DateTimeOffset.UtcNow;

        Client.ConnectionStateChanged += OnState;
        try
        {
            var error = await ThrowsAsync<AgoraClientException>(
                () => Client.LoginAsync(),
                "a sign-in with an unregistered App ID");

            Report($"failed after {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0}s: " +
                $"[{error.ErrorCode}] {error.Message}");
        }
        finally
        {
            Client.ConnectionStateChanged -= OnState;
        }

        // Observed, not asserted, for the same reason as Signaling: Chat only reports connection
        // states once a link attempt actually begins, and a sign-in the service refuses up front
        // never gets that far.
        lock (states)
        {
            Report(states.Count > 0
                ? $"observed {states.Count} connection state change(s)"
                : "no connection state change before the rejection — expected for an up-front refusal");
        }

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after a sign-in that should have failed.");
    }

    private static async Task LogoutWithoutALoginIsANoOp()
    {
        // Returns without crossing the bridge when there is no session — so, unlike the RTC
        // products' Leave, this cannot hang waiting for a callback that never comes.
        await Client.LogoutAsync();
        await Client.LogoutAsync();

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after LogoutAsync.");
    }

    private static void DisposesCleanly()
    {
        Client.Dispose();
        // IDisposable.Dispose must tolerate being called more than once.
        Client.Dispose();

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after Dispose.");
    }
#elif AGORA_SIGNALING
    private static void MissingAppIdIsRejected()
    {
        // The one call in this façade's own code (rather than the bindings') that validates
        // before touching a native client — Signaling requires a user ID too.
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions()),
            "a missing App ID");
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions { AppId = AppId }),
            "a missing user ID");
    }

    private static void ConstructsTheClient()
    {
        _client = CreateClient(new AgoraClientOptions
        {
            AppId = AppId,
            UserId = "net-agora-devicetests",
            Timeout = JoinTimeout,
        });

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true before any login was attempted.");
    }

    private static void RenewsAToken()
    {
        // The empty case is the façade's own guard; the shaped case crosses into the SDK, whose
        // answer to a renewal outside a session is its business — not throwing is the façade's
        // contract.
        Throws<ArgumentException>(() => Client.RenewToken(" "), "a whitespace renew token");

        Client.RenewToken(AppId);
    }

    private static async Task PublishBeforeLoginFails()
    {
        // RTM answers every operation through its own callback, so this is the cheapest full
        // round trip the suite has: the call crosses the bridge, the SDK rejects it (not logged
        // in), and the failure comes back as the façade's typed exception — no network, no
        // credentials.
        var error = await ThrowsAsync<AgoraClientException>(
            () => Client.PublishAsync(ChannelId, "hello"),
            "a publish before any login");

        Report($"publish rejected: [{error.ErrorCode}] {error.Message}");
    }

    private static async Task CancellationIsDistinctFromTimeout()
    {
        using var cancelSoon = new CancellationTokenSource();

        var login = Client.LoginAsync(cancelSoon.Token);

        // Cancelled synchronously, before any response could arrive, so "the caller's token
        // wins" is deterministic rather than a race against service latency — same reasoning as
        // the RTC flavours.
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => login,
            "a login cancelled through its own token");
    }

    private static async Task LoginFailsWithinTimeout()
    {
        // A failing login is also the one moment this credential-less suite can see the
        // connection lifecycle move, so the event wiring is asserted here rather than in a check
        // of its own.
        var states = new List<AgoraConnectionState>();
        void OnState(object? sender, AgoraConnectionStateEventArgs e)
        {
            lock (states)
            {
                states.Add(e.State);
            }
            Report($"connection state: {e.State} (reason {e.Reason})");
        }

        var started = DateTimeOffset.UtcNow;

        Client.ConnectionStateChanged += OnState;
        try
        {
            var error = await ThrowsAsync<AgoraClientException>(
                () => Client.LoginAsync(),
                "a login with an unregistered App ID");

            Report($"failed after {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0}s: " +
                $"[{error.ErrorCode}] {error.Message}");
        }
        finally
        {
            Client.ConnectionStateChanged -= OnState;
        }

        // Observed, not asserted, unlike the RTC flavours: RTM only starts reporting connection
        // states once a link attempt actually begins, and a login the SDK rejects up front (an
        // unregistered App ID) never gets that far — seen on the first run of this suite.
        lock (states)
        {
            Report(states.Count > 0
                ? $"observed {states.Count} connection state change(s)"
                : "no connection state change before the rejection — expected for an up-front refusal");
        }

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after a login that should have failed.");
    }

    private static void LogoutWithoutALoginIsANoOp()
    {
        Client.Logout();
        Client.Logout();

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after Logout.");
    }

    private static void DisposesCleanly()
    {
        Client.Dispose();
        // IDisposable.Dispose must tolerate being called more than once.
        Client.Dispose();

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after Dispose.");
    }
#else
    private static void MissingAppIdIsRejected()
    {
        // The one call in this façade's own code (rather than the bindings') that validates before
        // touching a native engine — see AgoraVideoOptions.Validate. Covered directly in
        // Net.Agora.UnitTests too; what only a device can prove is that both platform constructors
        // actually call it before creating the engine, rather than after.
        Throws<ArgumentException>(
            () => CreateClient(new AgoraClientOptions()),
            "a missing App ID");
    }

    private static void ConstructsTheClient()
    {
        // Live-broadcasting with an explicit Broadcaster role, deliberately: that is the one
        // combination where the constructor must call the engine's SetClientRole (the engine's
        // own default in this profile is Audience), so constructing this way is what proves the
        // role actually reaches the native side — the exact wiring that was once silently missing
        // from the Video façade.
        _client = CreateClient(new AgoraClientOptions
        {
            AppId = AppId,
            Timeout = JoinTimeout,
            ChannelProfile = AgoraChannelProfile.LiveBroadcasting,
            ClientRole = AgoraClientRole.Broadcaster,
        });

        Assert(!Client.IsJoined, "IsJoined is true before any join was attempted.");
        Assert(Client.LocalUid == 0, $"LocalUid is {Client.LocalUid} before any join was attempted.");
    }

    private static void EnablesAndDisablesMedia()
    {
        // None of these should throw even without camera/microphone permission granted — a defence
        // an app relies on to not crash for a permission it has not been granted yet. What is
        // proven here is that this façade does not add a throw of its own on top.
#if AGORA_VOICE
        Client.MuteLocalAudio(true);
        Client.MuteLocalAudio(false);
        Client.SetSpeakerphone(true);
        Client.SetSpeakerphone(false);
        Client.EnableVolumeIndication(TimeSpan.FromMilliseconds(200));

        // The façade's own guard, distinct from the SDK's error code for the same input — see
        // IAgoraVoiceClient.EnableVolumeIndication.
        Throws<ArgumentOutOfRangeException>(
            () => Client.EnableVolumeIndication(TimeSpan.Zero),
            "a zero volume-indication interval");
#else
        Client.EnableVideo();
        Client.DisableVideo();
        Client.EnableVideo();
        Client.MuteLocalAudio(true);
        Client.MuteLocalAudio(false);
        Client.MuteLocalVideo(true);
        Client.MuteLocalVideo(false);
        Client.SetSpeakerphone(true);
        Client.SetSpeakerphone(false);
        Client.EnableVolumeIndication(TimeSpan.FromMilliseconds(200));

        // The façade's own guard, distinct from the SDK's error code for the same input — see
        // IAgoraVideoClient.EnableVolumeIndication. SwitchCamera and StartPreview are deliberately
        // not called: they are the first calls that touch the camera, which on a headless
        // simulator raises a TCC permission prompt nobody is there to answer — the platform
        // binding suites cover them.
        Throws<ArgumentOutOfRangeException>(
            () => Client.EnableVolumeIndication(TimeSpan.Zero),
            "a zero volume-indication interval");
#endif
    }

    private static void RenewsAToken()
    {
        // The empty case is the façade's own guard; the shaped case crosses into the engine,
        // whose answer to a renewal outside a channel is its business — not throwing is the
        // façade's contract.
        Throws<ArgumentException>(() => Client.RenewToken(" "), "a whitespace renew token");

        Client.RenewToken(AppId);
    }

    private static async Task SecondJoinWhilePendingIsRejected()
    {
        // JoinAsync's synchronous prefix — everything up to its first genuine await — runs eagerly
        // when called, so by the time this line returns a Task, _pending is already set and a
        // second call is guaranteed to see it, not a race.
        using var cancelSoon = new CancellationTokenSource();
        var first = Client.JoinAsync(ChannelId, cancelSoon.Token);

        await ThrowsAsync<InvalidOperationException>(
            () => Client.JoinAsync(ChannelId),
            "a second join while the first is still pending");

        // Let the first attempt settle before the next check starts a fresh join — JoinAsync's
        // double-join guard is keyed on _pending, which only clears once whatever is in flight
        // finishes (see its finally block), so leaving this one dangling would fail every later
        // check for the same reason as the assertion above. Cancelled immediately, on this thread,
        // rather than on a timer: a real Agora server has been observed rejecting an unregistered
        // App ID in under 100ms (see JoinFailsWithinTimeout's reported duration), which raced and
        // beat a 200ms timer here in testing — cancelling synchronously, before JoinCore's fire-
        // and-forget native call could possibly get a response, is what makes this deterministic
        // rather than a coin flip against server latency.
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => first,
            "the first (still-pending) join, once cancelled");
    }

    private static void ExtensionsAreAvailable()
    {
        // The point of this check is what it can only fail at runtime: every call below compiles
        // and links whether or not the extension's native payload is in the app. Confirmed by
        // building the same app with -p:AgoraReferenceExtensions=false, where SetLowLightEnhance
        // is refused with code -2 and this check fails.
        //
        // It is a lower bound, not a full audit. In that same run the AINS and audio-preset
        // switches still answered 0, because the engine only goes looking for those plugins when
        // the audio pipeline actually starts — which this credential-free suite never reaches.
        // They are still called: a regression that turned one of them into an error would show up
        // here even though their success proves nothing.
        //
        // Before a join, deliberately: an extension is a media-pipeline switch, not a channel
        // operation, and none of these needs a connection — which keeps the check credential-free
        // like the rest of the suite.
        Client.SetNoiseSuppression(AgoraNoiseSuppression.Aggressive);
        Client.SetNoiseSuppression(AgoraNoiseSuppression.Off);

        Client.SetVoiceBeautifier(AgoraVoiceBeautifier.Fresh);
        Client.SetVoiceBeautifier(AgoraVoiceBeautifier.Off);

        // Mutually exclusive with the beautifier — set after it, so the last write wins and the
        // engine is left clean either way.
        Client.SetAudioEffect(AgoraAudioEffect.Studio);
        Client.SetAudioEffect(AgoraAudioEffect.Off);

#if !AGORA_VOICE
        // Video-only: the voice engine has no pipeline for these to act on, and its facade does
        // not expose them.
        //
        // The denoiser is the one asserted everywhere: an Android emulator, an iOS simulator and a
        // macOS host all accept it when the ClearVision payload is present and refuse it when it is
        // not, which is what makes it the payload's proof. It and the two enhancers ship in the
        // same framework, so accepting it is what says the framework loaded.
        Client.SetVideoDenoiser(true);

#if MACOS
        // The two *enhancers* are reported rather than asserted on macOS. They need a working video
        // pipeline, not just the payload: a GitHub macos-15 runner is a headless VM with no camera,
        // and it refuses SetLowLightEnhance with -1 there while accepting the denoiser from the same
        // framework — measured, on the run that added this leg. A developer's Mac accepts all three.
        // Asserting would fail the suite on the hardware it runs on rather than on a regression,
        // which is the same reason the virtual background and face detection are reported below.
        Report($"low-light enhance: {Attempt(() => Client.SetLowLightEnhance(true))}");
        Report($"colour enhance: {Attempt(() => Client.SetColorEnhance(true))}");
        Attempt(() => Client.SetColorEnhance(false));
        Attempt(() => Client.SetLowLightEnhance(false));
#else
        Client.SetLowLightEnhance(true);
        Client.SetColorEnhance(true);

        Client.SetColorEnhance(false);
        Client.SetLowLightEnhance(false);
#endif

        Client.SetVideoDenoiser(false);

        // Reported rather than asserted. These two answer -4 ("not supported") for a device that
        // cannot run the feature *and* for a missing payload, and an emulator is exactly such a
        // device: the Android emulator refuses the virtual background at -4 with the package
        // referenced and its .so in the APK. Asserting here would make the suite fail on the
        // hardware it runs on rather than on a real regression.
        Report($"virtual background: {Attempt(() => Client.SetVirtualBackground(AgoraVirtualBackground.Blurred()))}");
        Attempt(() => Client.SetVirtualBackground(null));
        Report($"face detection: {Attempt(() => Client.EnableFaceDetection(true))}");
        Attempt(() => Client.EnableFaceDetection(false));
#endif

        Report("every asserted extension switch answered 0");
    }

    /// <summary>
    /// Runs an extension switch whose refusal is not conclusive, and describes what happened
    /// instead of failing the check.
    /// </summary>
    private static string Attempt(Action call)
    {
        try
        {
            call();
            return "accepted";
        }
        catch (AgoraClientException exception)
        {
            return $"refused ({exception.ErrorCode}) — expected on hardware without the feature";
        }
    }

    private static async Task CancellationIsDistinctFromTimeout()
    {
        using var cancelSoon = new CancellationTokenSource();

        var join = Client.JoinAsync(ChannelId, cancelSoon.Token);

        // Cancelled immediately rather than on a timer, and for the same reason as the previous
        // check: a real server response has been observed arriving in well under 200ms, which
        // raced a fixed delay here rather than reliably losing to it. Cancelling synchronously,
        // right after the join starts and before any response could arrive, is what makes "the
        // caller's token wins" deterministic instead of a race against network latency.
        cancelSoon.Cancel();

        await ThrowsAsync<OperationCanceledException>(
            () => join,
            "a join cancelled through its own token");
    }

    private static async Task JoinFailsWithinTimeout()
    {
        // No cancellation token this time: whatever fails the join, it has to be either the SDK
        // reporting an error (an unregistered App ID) or the façade's own timeout — the two
        // reasons the façade's exception carries, as opposed to the OperationCanceledException the
        // previous check pinned to a caller-supplied token.
        var started = DateTimeOffset.UtcNow;

        // A failing join is also the one moment this credential-less suite can see the connection
        // lifecycle move (idle → connecting, and onward to failed), so the event wiring is
        // asserted here rather than in a check of its own.
        var states = new List<AgoraConnectionState>();
        void OnState(object? sender, AgoraConnectionStateEventArgs e)
        {
            lock (states)
            {
                states.Add(e.State);
            }
            Report($"connection state: {e.State} (reason {e.Reason})");
        }

        Client.ConnectionStateChanged += OnState;
        try
        {
            var error = await ThrowsAsync<AgoraClientException>(
                () => Client.JoinAsync(ChannelId),
                "a join with an unregistered App ID");

            Report($"failed after {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0}s: " +
                $"[{error.ErrorCode}] {error.Message}");
        }
        finally
        {
            Client.ConnectionStateChanged -= OnState;
        }

        lock (states)
        {
            Assert(states.Count > 0, "no ConnectionStateChanged event was raised during a failing join.");
        }

        Assert(!Client.IsJoined, "IsJoined is true after a join that should have failed.");
    }

    private static void LeaveAfterAFailedJoinIsANoOp()
    {
        // JoinAsync already calls LeaveCore on the way out of a failed join (see
        // its catch block) — this proves calling Leave again on top of that, the way an app's error
        // handler naturally would, is still safe.
        Client.Leave();
        Client.Leave();

        Assert(!Client.IsJoined, "IsJoined is true after Leave.");
        Assert(Client.LocalUid == 0, $"LocalUid is {Client.LocalUid} after Leave.");
    }

    private static void DisposesCleanly()
    {
        Client.Dispose();
        // IDisposable.Dispose must tolerate being called more than once.
        Client.Dispose();

        Assert(!Client.IsJoined, "IsJoined is true after Dispose.");
    }
#endif

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<TException>(Action action, string what)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{what} threw {exception.GetType().Name} rather than {typeof(TException).Name}.");
        }

        throw new InvalidOperationException($"{what} was accepted instead of throwing.");
    }

    private static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string what)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException expected)
        {
            return expected;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{what} threw {exception.GetType().Name} rather than {typeof(TException).Name}.");
        }

        throw new InvalidOperationException($"{what} was accepted instead of throwing.");
    }
}
