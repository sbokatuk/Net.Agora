using Agora.Rtc;
using Agora.Rtc.Video;
using Android.Content;
using Android.Views;

namespace Net.Agora.Video;

public sealed partial class AgoraVideoClient
{
    private readonly RtcEngine _engine;
    private readonly Handler _handler;

    /// <summary>
    /// Creates the engine and joins nothing yet — call <see cref="IAgoraVideoClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraVideoOptions.AppId"/> set.</param>
    /// <param name="context">
    /// Agora's <c>RtcEngineConfig</c> needs a <c>Context</c> to create the engine and to construct
    /// the <c>View</c>s <c>SetLocalView</c>/<c>SetRemoteView</c> render into. An
    /// <c>Activity</c> is not required by the engine itself, but the camera preview surface
    /// generally needs one — see Net.Agora.Video.Maui, which supplies it in a MAUI app.
    /// </param>
    public AgoraVideoClient(AgoraVideoOptions options, Context context)
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
            ?? throw new AgoraVideoException("RtcEngine.Create returned null.", errorCode: 0);

        // The role only exists in live-broadcasting, where the engine's default is Audience — a
        // Broadcaster who skipped this would join silently unable to publish.
        if (options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting)
        {
            _engine.SetClientRole((int)options.ClientRole);
        }
    }

    /// <summary>Renders this device's own camera feed into <paramref name="view"/>.</summary>
    public void SetLocalView(View view) =>
        _engine.SetupLocalVideo(new VideoCanvas(view, VideoCanvas.RenderModeHidden, 0));

    /// <summary>Renders a remote user's video into <paramref name="view"/> — call after <see cref="IAgoraVideoClient.UserJoined"/>.</summary>
    public void SetRemoteView(uint uid, View view) =>
        _engine.SetupRemoteVideo(new VideoCanvas(view, VideoCanvas.RenderModeHidden, (int)uid));

    /// <inheritdoc cref="IAgoraVideoClient.EnableVideo" />
    public void EnableVideo() => _engine.EnableVideo();

    /// <inheritdoc cref="IAgoraVideoClient.DisableVideo" />
    public void DisableVideo() => _engine.DisableVideo();

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => _engine.MuteLocalAudioStream(mute);

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalVideo" />
    public void MuteLocalVideo(bool mute) => _engine.MuteLocalVideoStream(mute);

    /// <inheritdoc cref="IAgoraVideoClient.StartPreview" />
    public void StartPreview() => _engine.StartPreview();

    /// <inheritdoc cref="IAgoraVideoClient.StopPreview" />
    public void StopPreview() => _engine.StopPreview();

    /// <inheritdoc cref="IAgoraVideoClient.SwitchCamera" />
    public void SwitchCamera() => _engine.SwitchCamera();

    /// <inheritdoc cref="IAgoraVideoClient.SetSpeakerphone" />
    public void SetSpeakerphone(bool speakerphone)
    {
        // Two native calls behind one switch: the live override only works once the audio session
        // exists (in a channel), the default-route call is what applies before one does. The
        // Java method's casing ("Routeto") is Agora's own wart, faithfully preserved.
        if (IsJoined)
        {
            _engine.SetEnableSpeakerphone(speakerphone);
        }
        else
        {
            _engine.SetDefaultAudioRoutetoSpeakerphone(speakerphone);
        }
    }

    /// <inheritdoc cref="IAgoraVideoClient.RenewToken" />
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
    private sealed class Handler(AgoraVideoClient owner) : IRtcEngineEventHandler
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

        public override void OnUserMuteVideo(int uid, bool muted) =>
            owner.RaiseRemoteVideoMuted((uint)uid, muted);

        public override void OnAudioVolumeIndication(
            IRtcEngineEventHandler.AudioVolumeInfo[] speakers, int totalVolume)
        {
            var mapped = speakers is { Length: > 0 }
                ? Array.ConvertAll(speakers, s => new AgoraSpeakerVolume((uint)s.Uid, s.Volume))
                : [];
            owner.RaiseVolumeIndication(mapped, totalVolume);
        }

        public override void OnConnectionStateChanged(int state, int reason) =>
            owner.RaiseConnectionStateChanged((AgoraConnectionState)state, reason);

        public override void OnTokenPrivilegeWillExpire(string token) =>
            owner.RaiseTokenPrivilegeWillExpire();

        public override void OnError(int err) =>
            owner.RaiseError(RtcEngine.GetErrorDescription(err) ?? $"Agora error {err}", err);
    }

    // ------------------------------------------------------------------------------------------
    // Extension controls — see the shared half for why the return codes are checked.
    // ------------------------------------------------------------------------------------------

    private int SetNoiseSuppressionCore(AgoraNoiseSuppression mode) =>
        mode == AgoraNoiseSuppression.Off
            ? _engine.SetAINSMode(false, (int)AgoraNoiseSuppression.Balanced)
            : _engine.SetAINSMode(true, (int)mode);

    private int SetVoiceBeautifierCore(AgoraVoiceBeautifier preset) =>
        _engine.SetVoiceBeautifierPreset((int)preset);

    private int SetAudioEffectCore(AgoraAudioEffect preset) =>
        _engine.SetAudioEffectPreset((int)preset);

    private int SetVirtualBackgroundCore(AgoraVirtualBackground? background)
    {
        // Both parameters are required on this platform, unlike iOS where either may be nil, so a
        // disable still passes a source object — its contents are ignored when enabled is false.
        var source = new VirtualBackgroundSource();
        if (background is not null)
        {
            source.BackgroundSourceType = background.SourceType;
            source.Color = (int)background.Color;
            source.Source = background.Path;
            source.BlurDegree = (int)background.Blur;
        }

        return _engine.EnableVirtualBackground(background is not null, source, new SegmentationProperty());
    }

    private int SetVideoDenoiserCore(bool enabled) =>
        _engine.SetVideoDenoiserOptions(enabled, new VideoDenoiserOptions());

    private int SetLowLightEnhanceCore(bool enabled) =>
        _engine.SetLowlightEnhanceOptions(enabled, new LowLightEnhanceOptions());

    private int SetColorEnhanceCore(bool enabled) =>
        _engine.SetColorEnhanceOptions(enabled, new ColorEnhanceOptions());

    private int EnableFaceDetectionCore(bool enabled) => _engine.EnableFaceDetection(enabled);
}
