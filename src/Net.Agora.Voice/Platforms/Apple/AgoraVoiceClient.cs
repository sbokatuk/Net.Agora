using Net.Agora.Voice.iOS;

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

        _delegate = new Delegate(this);
        var config = new AgoraRtcEngineConfig
        {
            AppId = options.AppId,
            ChannelProfile = options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting
                ? Net.Agora.Voice.iOS.AgoraChannelProfile.LiveBroadcasting
                : Net.Agora.Voice.iOS.AgoraChannelProfile.Communication,
        };
        _engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        // The role only exists in live-broadcasting, where the engine's default is Audience — a
        // Broadcaster who skipped this would join silently unable to publish.
        if (options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting)
        {
            _engine.SetClientRole(options.ClientRole == AgoraClientRole.Audience
                ? Net.Agora.Voice.iOS.AgoraClientRole.Audience
                : Net.Agora.Voice.iOS.AgoraClientRole.Broadcaster);
        }

        if (options.DefaultToSpeakerphone)
        {
            _engine.SetDefaultAudioRouteToSpeakerphone(true);
        }
    }

    /// <inheritdoc cref="IAgoraVoiceClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => _engine.MuteLocalAudioStream(mute);

    /// <inheritdoc cref="IAgoraVoiceClient.SetSpeakerphone" />
    public void SetSpeakerphone(bool speakerphone)
    {
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

        public override void TokenPrivilegeWillExpire(AgoraRtcEngineKit engine, string token) =>
            owner.RaiseTokenPrivilegeWillExpire();

        public override void DidOccurError(AgoraRtcEngineKit engine, Net.Agora.Voice.iOS.AgoraErrorCode errorCode) =>
            owner.RaiseError($"Agora error {errorCode}", (int)errorCode);
    }
}
