// One Apple source set for both iOS and native macOS. The AgoraRtcEngineKit surface is the same on
// both; the only differences are the canvas view type (UIView vs NSView, aliased as NativeView) and
// a handful of iOS-only engine calls the macOS binding does not expose (speakerphone routing,
// switchCamera). The binding namespace differs per platform, aliased as Rtc so the qualified
// references below read the same either way.
#if MACOS
using Net.Agora.Video.Mac;
using AppKit;
using NativeView = AppKit.NSView;
using Rtc = Net.Agora.Video.Mac;
#else
using Net.Agora.Video.iOS;
using UIKit;
using NativeView = UIKit.UIView;
using Rtc = Net.Agora.Video.iOS;
#endif

namespace Net.Agora.Video;

public sealed partial class AgoraVideoClient
{
    private readonly AgoraRtcEngineKit _engine;
    private readonly Delegate _delegate;

    /// <summary>
    /// Creates the engine and joins nothing yet — call <see cref="IAgoraVideoClient.JoinAsync"/>.
    /// Create the client after the hosting view has appeared (e.g. in <c>ViewDidAppear</c>), not
    /// in a constructor — <see cref="SetLocalView"/>/<see cref="SetRemoteView"/> add a subview to
    /// whatever native view you pass (a <c>UIView</c> on iOS, an <c>NSView</c> on macOS), which
    /// needs a window to render into.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraVideoOptions.AppId"/> set.</param>
    public AgoraVideoClient(AgoraVideoOptions options)
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
        }
        catch
        {
            // A half-built client is never disposed, so the slot has to come back here or the
            // process could never create another one.
            AgoraEngineSlot.Release();
            throw;
        }
    }

    /// <summary>Renders this device's own camera feed into <paramref name="view"/> — the SDK adds its own subview.</summary>
    public void SetLocalView(NativeView view) =>
        _engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = view });

    /// <summary>Renders a remote user's video into <paramref name="view"/> — call after <see cref="IAgoraVideoClient.UserJoined"/>.</summary>
    public void SetRemoteView(uint uid, NativeView view) =>
        _engine.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = uid, View = view });

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
    public void SwitchCamera()
    {
#if !MACOS
        _engine.SwitchCamera();
#endif
        // No-op on macOS: switchCamera is an iOS front/back-camera flip the desktop engine does not
        // implement. A Mac selects its capture device by device selection, not a flip.
    }

    /// <inheritdoc cref="IAgoraVideoClient.SetSpeakerphone" />
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

    /// <inheritdoc cref="IAgoraVideoClient.RenewToken" />
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
    private sealed class Delegate(AgoraVideoClient owner) : AgoraRtcEngineDelegate
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

        public override void DidVideoMuted(AgoraRtcEngineKit engine, bool muted, nuint uid) =>
            owner.RaiseRemoteVideoMuted((uint)uid, muted);

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

    private int SetVirtualBackgroundCore(AgoraVirtualBackground? background)
    {
        // Null for both is what the header documents for a disable, and passing null segData for
        // an enable takes the engine's own segmentation defaults — the Android side has no
        // equivalent and needs an object either way.
        if (background is null)
        {
            return (int)_engine.EnableVirtualBackground(false, null, null);
        }

        var source = new AgoraVirtualBackgroundSource
        {
            BackgroundSourceType = (AgoraVirtualBackgroundSourceType)(ulong)background.SourceType,
            Color = background.Color,
            Source = background.Path,
            BlurDegree = (AgoraBlurDegree)(ulong)background.Blur,
        };

        return (int)_engine.EnableVirtualBackground(true, source, null);
    }

    private int SetVideoDenoiserCore(bool enabled) =>
        (int)_engine.SetVideoDenoiserOptions(enabled, new AgoraVideoDenoiserOptions());

    private int SetLowLightEnhanceCore(bool enabled) =>
        (int)_engine.SetLowlightEnhanceOptions(enabled, new AgoraLowlightEnhanceOptions());

    private int SetColorEnhanceCore(bool enabled) =>
        (int)_engine.SetColorEnhanceOptions(enabled, new AgoraColorEnhanceOptions());

    private int EnableFaceDetectionCore(bool enabled) => (int)_engine.EnableFaceDetection(enabled);
}
