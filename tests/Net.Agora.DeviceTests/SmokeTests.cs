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
#elif AGORA_WHITEBOARD
        new("a missing identifier is rejected before any native call", MissingAppIdIsRejected),
        new("constructs the SDK against a board view", ConstructsTheClient),
        new("drawing before a join is rejected by the client, not the board", DrawBeforeJoinIsRejected),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered room fails the join within the configured timeout", LoginFailsWithinTimeout),
        new("disconnect without a join is a no-op", LogoutWithoutALoginIsANoOp),
        new("disposes cleanly", DisposesCleanly),
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
        // not expose them. These three are the ones whose refusal is unambiguous — an Android
        // emulator and an iOS simulator both accept them when the ClearVision payload is present
        // and refuse them when it is not.
        Client.SetVideoDenoiser(true);
        Client.SetLowLightEnhance(true);
        Client.SetColorEnhance(true);

        Client.SetColorEnhance(false);
        Client.SetLowLightEnhance(false);
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
