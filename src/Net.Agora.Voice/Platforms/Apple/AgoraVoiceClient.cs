// One Apple source set for both iOS and native macOS — the AgoraRtcEngineKit audio surface is the
// same on both. The binding namespace differs per platform, aliased as Rtc so the qualified
// references below read the same either way; the only behavioural difference is speakerphone
// routing, an iOS-only concept guarded with #if MACOS below.
#if MACOS
using Net.Agora.Voice.Mac;
using Rtc = Net.Agora.Voice.Mac;
#else
using Net.Agora.Voice.iOS;
using Rtc = Net.Agora.Voice.iOS;
#endif

namespace Net.Agora.Voice;

public sealed partial class AgoraVoiceClient
{
    private readonly AgoraRtcEngineKit _engine;
    private readonly Delegate _delegate;

    /// <summary>
    /// Creates the engine and joins nothing yet — call <see cref="IAgoraVoiceClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraVoiceOptions.AppId"/> set.</param>
    public AgoraVoiceClient(AgoraVoiceOptions options)
    {
        options.Validate();
        _options = options;

        AgoraEngineSlot.Acquire();
        try
        {
            _delegate = new Delegate(this);
            var config = new AgoraRtcEngineConfig
            {
                AppId = options.AppId,
                ChannelProfile = options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting
                    ? Rtc.AgoraChannelProfile.LiveBroadcasting
                    : Rtc.AgoraChannelProfile.Communication,
            };
            _engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

            // The role only exists in live-broadcasting, where the engine's default is Audience — a
            // Broadcaster who skipped this would join silently unable to publish.
            if (options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting)
            {
                _engine.SetClientRole(options.ClientRole == AgoraClientRole.Audience
                    ? Rtc.AgoraClientRole.Audience
                    : Rtc.AgoraClientRole.Broadcaster);
            }

#if !MACOS
            // Speakerphone routing is iOS-only — the macOS engine does not implement it (a Mac routes
            // audio by output device, not an earpiece/speaker toggle), so this option is a no-op there.
            if (options.DefaultToSpeakerphone)
            {
                _engine.SetDefaultAudioRouteToSpeakerphone(true);
            }
#endif
        }
        catch
        {
            // A half-built client is never disposed, so the slot has to come back here or the
            // process could never create another one.
            AgoraEngineSlot.Release();
            throw;
        }
    }

    /// <inheritdoc cref="IAgoraVoiceClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => _engine.MuteLocalAudioStream(mute);

    /// <inheritdoc cref="IAgoraVoiceClient.SetSpeakerphone" />
    public void SetSpeakerphone(bool speakerphone)
    {
#if MACOS
        // No-op on macOS: speakerphone routing is an iOS audio-session concept the desktop engine
        // does not implement (the selectors are unrecognized there). A Mac routes audio by output
        // *device*, not an earpiece/speaker toggle.
        _ = speakerphone;
#else
        // Two native calls behind one switch: setEnableSpeakerphone answers -3 (not ready) until
        // the audio session exists (observed on the simulator suite in sbokatuk/Net.Agora.iOS),
        // and setDefaultAudioRouteToSpeakerphone is what applies before one does.
        if (IsJoined)
        {
            _engine.SetEnableSpeakerphone(speakerphone);
        }
        else
        {
            _engine.SetDefaultAudioRouteToSpeakerphone(speakerphone);
        }
#endif
    }

    /// <inheritdoc cref="IAgoraVoiceClient.RenewToken" />
    public void RenewToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _engine.RenewToken(token);
    }

    private void EnableVolumeIndicationCore(int intervalMilliseconds) =>
        _engine.EnableAudioVolumeIndication(intervalMilliseconds, smooth: 3, reportVad: false);

    private void JoinCore(string channelId) =>
        _engine.JoinChannel(_options.Token, channelId, null, _options.Uid, joinSuccess: null);

    private void LeaveCore() => _engine.LeaveChannel(null);

    private void DisposeCore() => AgoraRtcEngineKit.Destroy();

    /// <summary>
    /// Translates <c>AgoraRtcEngineDelegate</c>'s (all-optional) callbacks into the shared
    /// partial's Raise* calls. join/leave success is also reported through the block parameters
    /// on JoinChannel/LeaveChannel, but the delegate is used uniformly here so both entry points
    /// (the initial join and a server-initiated rejoin) go through the same path.
    /// </summary>
    private sealed class Delegate(AgoraVoiceClient owner) : AgoraRtcEngineDelegate
    {
        public override void DidJoinChannel(AgoraRtcEngineKit engine, string channel, nuint uid, nint elapsed) =>
            owner.RaiseJoined((uint)uid);

        public override void DidLeaveChannel(AgoraRtcEngineKit engine, AgoraChannelStats stats) =>
            owner.RaiseLeft();

        public override void DidJoinedOfUid(AgoraRtcEngineKit engine, nuint uid, nint elapsed) =>
            owner.RaiseUserJoined((uint)uid);

        public override void DidOfflineOfUid(AgoraRtcEngineKit engine, nuint uid, AgoraUserOfflineReason reason) =>
            owner.RaiseUserOffline((uint)uid);

        public override void DidAudioMuted(AgoraRtcEngineKit engine, bool muted, nuint uid) =>
            owner.RaiseRemoteAudioMuted((uint)uid, muted);

        public override void ReportAudioVolumeIndication(
            AgoraRtcEngineKit engine, AgoraRtcAudioVolumeInfo[] speakers, nint totalVolume)
        {
            var mapped = speakers is { Length: > 0 }
                ? Array.ConvertAll(speakers, s => new AgoraSpeakerVolume((uint)s.Uid, (int)s.Volume))
                : [];
            owner.RaiseVolumeIndication(mapped, (int)totalVolume);
        }

        public override void ConnectionChangedToState(
            AgoraRtcEngineKit engine, Rtc.AgoraConnectionState state, nint reason) =>
            owner.RaiseConnectionStateChanged((AgoraConnectionState)(long)state, (int)reason);

        public override void TokenPrivilegeWillExpire(AgoraRtcEngineKit engine, string token) =>
            owner.RaiseTokenPrivilegeWillExpire();

        public override void DidOccurError(AgoraRtcEngineKit engine, Rtc.AgoraErrorCode errorCode) =>
            owner.RaiseError($"Agora error {errorCode}", (int)errorCode);
    }

    // ------------------------------------------------------------------------------------------
    // Extension controls — see the shared half for why the return codes are checked.
    // ------------------------------------------------------------------------------------------

    private int SetNoiseSuppressionCore(AgoraNoiseSuppression mode) =>
        mode == AgoraNoiseSuppression.Off
            ? (int)_engine.SetAinsMode(false, AgoraAinsMode.Balanced)
            : (int)_engine.SetAinsMode(true, (AgoraAinsMode)(long)mode);

    private int SetVoiceBeautifierCore(AgoraVoiceBeautifier preset) =>
        (int)_engine.SetVoiceBeautifierPreset((Rtc.AgoraVoiceBeautifierPreset)(long)preset);

    private int SetAudioEffectCore(AgoraAudioEffect preset) =>
        (int)_engine.SetAudioEffectPreset((Rtc.AgoraAudioEffectPreset)(long)preset);
}
