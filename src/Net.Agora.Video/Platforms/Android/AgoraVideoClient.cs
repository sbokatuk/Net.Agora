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

        public override void OnError(int err) =>
            owner.RaiseError(RtcEngine.GetErrorDescription(err) ?? $"Agora error {err}", err);
    }
}
