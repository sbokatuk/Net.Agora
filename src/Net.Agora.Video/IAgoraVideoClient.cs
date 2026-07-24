namespace Net.Agora.Video;

/// <summary>
/// Joins an Agora RTC channel and publishes/subscribes audio and video, with the same surface on
/// Android and iOS.
///
/// The platform bindings underneath do not resemble each other — Android's <c>RtcEngine</c> is a
/// Java abstract class overridden with a 100+ method event-handler class, iOS's
/// <c>AgoraRtcEngineKit</c> takes an Objective-C delegate — and this is the layer that hides the
/// difference. Reach for <c>Agora.Rtc.*</c> (Android, via Net.Agora.Video.Android) or
/// <c>Net.Agora.Video.iOS.*</c> directly for anything not exposed here.
///
/// Setting the local/remote render target is platform-specific (an Android <c>View</c> vs. a UIKit
/// <c>UIView</c>) and is therefore not part of this interface — call <c>SetLocalView</c> /
/// <c>SetRemoteView</c> on the concrete <see cref="AgoraVideoClient"/>.
/// </summary>
public interface IAgoraVideoClient : IDisposable
{
    /// <summary>This client joined <see cref="AgoraChannelEventArgs.ChannelId"/> successfully.</summary>
    event EventHandler<AgoraChannelEventArgs>? Joined;

    /// <summary>This client left the channel, whether by <see cref="Leave"/> or a connection drop.</summary>
    event EventHandler<AgoraChannelEventArgs>? Left;

    /// <summary>A remote user joined the channel — safe to call <c>SetRemoteView</c> for their uid.</summary>
    event EventHandler<AgoraUserEventArgs>? UserJoined;

    /// <summary>A remote user left the channel.</summary>
    event EventHandler<AgoraUserEventArgs>? UserOffline;

    /// <summary>A remote user muted or unmuted their microphone — the "who is muted" signal a call UI shows.</summary>
    event EventHandler<AgoraRemoteAudioMuteEventArgs>? RemoteAudioMuted;

    /// <summary>A remote user paused or resumed their camera — hide or show their view accordingly.</summary>
    event EventHandler<AgoraRemoteVideoMuteEventArgs>? RemoteVideoMuted;

    /// <summary>
    /// Who is speaking and how loudly, at the cadence <see cref="EnableVolumeIndication"/>
    /// configured. Silent until that call is made.
    /// </summary>
    event EventHandler<AgoraVolumeIndicationEventArgs>? VolumeIndication;

    /// <summary>
    /// The connection's lifecycle: connecting, connected, reconnecting after a drop, failed. The
    /// SDK reconnects on its own — <see cref="AgoraConnectionState.Reconnecting"/> is informational,
    /// <see cref="AgoraConnectionState.Failed"/> is when a fresh <see cref="JoinAsync"/> is needed.
    /// </summary>
    event EventHandler<AgoraConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// The channel token is about to expire — obtain a fresh one from your token server and pass
    /// it to <see cref="RenewToken"/>, or the client will be disconnected when it lapses.
    /// </summary>
    event EventHandler? TokenPrivilegeWillExpire;

    /// <summary>
    /// The SDK reported a problem. When it happens while <c>JoinAsync</c> is pending, the pending
    /// call fails with the same code as an <see cref="AgoraVideoException"/>.
    /// </summary>
    event EventHandler<AgoraVideoErrorEventArgs>? Error;

    /// <summary>True between a successful join and <see cref="Leave"/> or a drop.</summary>
    bool IsJoined { get; }

    /// <summary>The uid Agora assigned this client, or 0 when not joined.</summary>
    uint LocalUid { get; }

    /// <summary>
    /// Joins <paramref name="channelId"/>, completing when the server confirms the join.
    /// </summary>
    /// <exception cref="AgoraVideoException">
    /// The SDK reported an error, or did not confirm within <see cref="AgoraVideoOptions.Timeout"/>.
    /// </exception>
    Task JoinAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>Leaves the channel and releases the camera and microphone. Safe to call when idle.</summary>
    void Leave();

    /// <summary>Enables the local camera and starts sending video. Off by default.</summary>
    void EnableVideo();

    /// <summary>Disables the local camera.</summary>
    void DisableVideo();

    /// <summary>Mutes or unmutes the local microphone without releasing it.</summary>
    void MuteLocalAudio(bool mute);

    /// <summary>Pauses or resumes the local camera feed without releasing it.</summary>
    void MuteLocalVideo(bool mute);

    /// <summary>
    /// Starts rendering the local camera into the view given to <c>SetLocalView</c> before any
    /// join — the "check your hair" preview. <see cref="EnableVideo"/> first.
    /// </summary>
    void StartPreview();

    /// <summary>Stops the local preview started by <see cref="StartPreview"/>.</summary>
    void StopPreview();

    /// <summary>Toggles between the front and back camera while capturing.</summary>
    void SwitchCamera();

    /// <summary>
    /// Routes audio to the loudspeaker (true) or the earpiece (false). Callable before or during
    /// a call: before a join it sets the default route, during one it switches the live route.
    /// </summary>
    void SetSpeakerphone(bool speakerphone);

    /// <summary>
    /// Starts <see cref="VolumeIndication"/> reports every <paramref name="interval"/> (Agora
    /// recommends at least 200 ms). The SDK offers no way to stop them again short of leaving —
    /// its documented "pass zero to disable" answers an invalid-argument error in practice.
    /// </summary>
    void EnableVolumeIndication(TimeSpan interval);

    /// <summary>Replaces the channel token — see <see cref="TokenPrivilegeWillExpire"/>.</summary>
    void RenewToken(string token);
}
