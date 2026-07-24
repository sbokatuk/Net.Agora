using Agora.Rtc;
using Android.Content;

namespace Net.Agora.Voice;

public sealed partial class AgoraVoiceClient
{
    private readonly RtcEngine _engine;
    private readonly Handler _handler;

    /// <summary>
    /// Creates the engine and joins nothing yet — call <see cref="IAgoraVoiceClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraVoiceOptions.AppId"/> set.</param>
    /// <param name="context">
    /// Agora's <c>RtcEngineConfig</c> needs a <c>Context</c> to create the engine. Any context
    /// serves — unlike the video client there is no render surface to anchor to an Activity —
    /// but see Net.Agora.Voice.Maui, which supplies one in a MAUI app so callers need no
    /// platform code at all.
    /// </param>
    public AgoraVoiceClient(AgoraVoiceOptions options, Context context)
    {
        options.Validate();
        _options = options;

        _handler = new Handler(this);

        // RtcEngineConfig binds each of its Java fields (mAppId, mContext, ...) twice: once as the
        // plain field (MAppId, settable) and once through a same-named read-only getter method
        // (AppId) the SDK also exposes. Only the M-prefixed field form has a setter.
        var config = new RtcEngineConfig
        {
            MContext = context,
            MAppId = options.AppId,
            MChannelProfile = (int)options.ChannelProfile,
            MEventHandler = _handler,
        };
        _engine = RtcEngine.Create(config)
            ?? throw new AgoraVoiceException("RtcEngine.Create returned null.", errorCode: 0);

        // The role only exists in live-broadcasting, where the engine's default is Audience — a
        // Broadcaster who skipped this would join silently unable to publish.
        if (options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting)
        {
            _engine.SetClientRole((int)options.ClientRole);
        }

        if (options.DefaultToSpeakerphone)
        {
            // Agora's own Java casing ("Routeto") — a wart the binding faithfully preserves.
            _engine.SetDefaultAudioRoutetoSpeakerphone(true);
        }
    }

    /// <inheritdoc cref="IAgoraVoiceClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => _engine.MuteLocalAudioStream(mute);

    /// <inheritdoc cref="IAgoraVoiceClient.SetSpeakerphone" />
    public void SetSpeakerphone(bool speakerphone)
    {
        // Two native calls behind one switch: the live override only works once the audio session
        // exists (in a channel), the default-route call is what applies before one does.
        if (IsJoined)
        {
            _engine.SetEnableSpeakerphone(speakerphone);
        }
        else
        {
            _engine.SetDefaultAudioRoutetoSpeakerphone(speakerphone);
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
        _engine.JoinChannel(_options.Token, channelId, null, (int)_options.Uid);

    private void LeaveCore() => _engine.LeaveChannel();

    private void DisposeCore()
    {
        RtcEngine.Destroy();
    }

    /// <summary>
    /// Translates <c>IRtcEngineEventHandler</c>'s callbacks — a Java abstract class overridden
    /// per-instance, not a .NET event — into the shared partial's Raise* calls.
    /// </summary>
    private sealed class Handler(AgoraVoiceClient owner) : IRtcEngineEventHandler
    {
        public override void OnJoinChannelSuccess(string channel, int uid, int elapsed) =>
            owner.RaiseJoined((uint)uid);

        public override void OnLeaveChannel(IRtcEngineEventHandler.RtcStats stats) =>
            owner.RaiseLeft();

        public override void OnUserJoined(int uid, int elapsed) =>
            owner.RaiseUserJoined((uint)uid);

        public override void OnUserOffline(int uid, int reason) =>
            owner.RaiseUserOffline((uint)uid);

        public override void OnUserMuteAudio(int uid, bool muted) =>
            owner.RaiseRemoteAudioMuted((uint)uid, muted);

        public override void OnAudioVolumeIndication(
            IRtcEngineEventHandler.AudioVolumeInfo[] speakers, int totalVolume)
        {
            var mapped = speakers is { Length: > 0 }
                ? Array.ConvertAll(speakers, s => new AgoraSpeakerVolume((uint)s.Uid, s.Volume))
                : [];
            owner.RaiseVolumeIndication(mapped, totalVolume);
        }

        public override void OnTokenPrivilegeWillExpire(string token) =>
            owner.RaiseTokenPrivilegeWillExpire();

        public override void OnError(int err) =>
            owner.RaiseError(RtcEngine.GetErrorDescription(err) ?? $"Agora error {err}", err);
    }
}
