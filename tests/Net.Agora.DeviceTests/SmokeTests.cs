// One suite, two products: the same checks compile against the Video or the Voice façade (see
// the csproj's AgoraDeviceProduct), so the aliases below are the only per-product spelling. The
// few genuinely product-specific checks sit behind AGORA_VOICE.
#if AGORA_VOICE
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
        new("a missing App ID is rejected before any native call", MissingAppIdIsRejected),
        new("constructs a live-broadcasting broadcaster client", ConstructsTheClient),
#if AGORA_VOICE
        new("drives mute, speakerphone and volume indication without throwing", EnablesAndDisablesMedia),
#else
        new("drives the media, speakerphone and volume controls without throwing", EnablesAndDisablesMedia),
#endif
        new("an empty renew token is rejected, a shaped one is accepted", RenewsAToken),
        new("rejects a second join while one is pending", SecondJoinWhilePendingIsRejected),
        new("cancelling the token surfaces as OperationCanceledException", CancellationIsDistinctFromTimeout),
        new("an unregistered App ID fails the join within the configured timeout", JoinFailsWithinTimeout),
        new("leave after a failed join is a no-op", LeaveAfterAFailedJoinIsANoOp),
        new("disposes cleanly", DisposesCleanly),
    ];

    private static void Report(string message) => Reporter(message);

    private static AgoraClient CreateClient(AgoraClientOptions options)
    {
#if ANDROID
        return new AgoraClient(
            options,
            AndroidContext ?? throw new InvalidOperationException("SmokeTests.AndroidContext was not set."));
#else
        return new AgoraClient(options);
#endif
    }

    private static AgoraClient Client =>
        _client ?? throw new InvalidOperationException("the client has not been constructed yet.");

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
