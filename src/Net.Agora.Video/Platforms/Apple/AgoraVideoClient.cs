using Net.Agora.Video.iOS;
using UIKit;

namespace Net.Agora.Video;

public sealed partial class AgoraVideoClient
{
    private readonly AgoraRtcEngineKit _engine;
    private readonly Delegate _delegate;

    /// <summary>
    /// Creates the engine and joins nothing yet — call <see cref="IAgoraVideoClient.JoinAsync"/>.
    /// Create the client after the hosting view has appeared (e.g. in <c>ViewDidAppear</c>), not
    /// in a constructor — <see cref="SetLocalView"/>/<see cref="SetRemoteView"/> add a subview to
    /// whatever <see cref="UIView"/> you pass, which needs a window to render into.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraVideoOptions.AppId"/> set.</param>
    public AgoraVideoClient(AgoraVideoOptions options)
    {
        options.Validate();
        _options = options;

        _delegate = new Delegate(this);
        var config = new AgoraRtcEngineConfig
        {
            AppId = options.AppId,
            ChannelProfile = options.ChannelProfile == AgoraChannelProfile.LiveBroadcasting
                ? Net.Agora.Video.iOS.AgoraChannelProfile.LiveBroadcasting
                : Net.Agora.Video.iOS.AgoraChannelProfile.Communication,
        };
        _engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);
    }

    /// <summary>Renders this device's own camera feed into <paramref name="view"/> — the SDK adds its own subview.</summary>
    public void SetLocalView(UIView view) =>
        _engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = view });

    /// <summary>Renders a remote user's video into <paramref name="view"/> — call after <see cref="IAgoraVideoClient.UserJoined"/>.</summary>
    public void SetRemoteView(uint uid, UIView view) =>
        _engine.SetupRemoteVideo(new AgoraRtcVideoCanvas { Uid = uid, View = view });

    /// <inheritdoc cref="IAgoraVideoClient.EnableVideo" />
    public void EnableVideo() => _engine.EnableVideo();

    /// <inheritdoc cref="IAgoraVideoClient.DisableVideo" />
    public void DisableVideo() => _engine.DisableVideo();

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => _engine.MuteLocalAudioStream(mute);

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalVideo" />
    public void MuteLocalVideo(bool mute) => _engine.MuteLocalVideoStream(mute);

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

        public override void DidOccurError(AgoraRtcEngineKit engine, Net.Agora.Video.iOS.AgoraErrorCode errorCode) =>
            owner.RaiseError($"Agora error {errorCode}", (int)errorCode);
    }
}
