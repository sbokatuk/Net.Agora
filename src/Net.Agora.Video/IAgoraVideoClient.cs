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
/// <remarks>
/// <para>
/// <strong>One live client per process.</strong> Both SDKs' engines are process-wide singletons
/// (<c>sharedEngineWithConfig:delegate:</c> / <c>RtcEngine.create</c>, torn down by a static
/// destroy), so constructing a second client while one is alive throws
/// <see cref="InvalidOperationException"/> rather than silently stealing the first one's
/// callbacks. Dispose the client you have before creating another; a two-page app should share
/// one instance rather than construct one per page.
/// </para>
/// <para>
/// <strong>Events arrive on SDK threads.</strong> Every event below is raised on whichever thread
/// the native SDK chose, not the UI thread. Marshal before touching UI — in MAUI that is
/// <c>MainThread.BeginInvokeOnMainThread</c>.
/// </para>
/// </remarks>
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

    // ------------------------------------------------------------------------------------------
    // Extension controls
    //
    // Every call below is a switch on the engine, and every one of them needs a native payload
    // that neither RTC package carries — Agora ships the optional features as separate artifacts,
    // and each has its own Net.Agora.Extensions.* package pair. The switch is always callable; it
    // is the *effect* that is missing without the package, and the SDKs report that as an ordinary
    // failure code, which these methods raise as an exception rather than swallow.
    //
    // Not every extension has a facade call. The spatial-audio engine, content inspection and face
    // capture each need a surface of their own; the video-quality analyser and the software
    // encoders are chosen by the engine itself once present, so there is nothing to call. Their
    // packages still ship, and the platform bindings still expose them.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Turns the AI noise suppressor on at the given aggressiveness, or off.
    /// Needs <c>Net.Agora.Extensions.Ains.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <exception cref="AgoraVideoException">
    /// The SDK refused — most often because the extension package is not referenced.
    /// </exception>
    void SetNoiseSuppression(AgoraNoiseSuppression mode);

    /// <summary>
    /// Applies a voice-timbre preset, or <see cref="AgoraVoiceBeautifier.Off"/> to clear it.
    /// Needs <c>Net.Agora.Extensions.AudioBeauty.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <remarks>Overwrites any <see cref="SetAudioEffect"/> preset, and is overwritten by one.</remarks>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetVoiceBeautifier(AgoraVoiceBeautifier preset);

    /// <summary>
    /// Applies a room-acoustics or voice-changer preset, or <see cref="AgoraAudioEffect.Off"/> to
    /// clear it. Needs <c>Net.Agora.Extensions.AudioBeauty.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <remarks>Overwrites any <see cref="SetVoiceBeautifier"/> preset, and is overwritten by one.</remarks>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetAudioEffect(AgoraAudioEffect preset);

    /// <summary>
    /// Puts a blurred, coloured or pictured background behind the local camera's subject, or
    /// removes it with null. Needs <c>Net.Agora.Extensions.VirtualBackground.Android</c> /
    /// <c>.iOS</c>, and the camera to be running.
    /// </summary>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetVirtualBackground(AgoraVirtualBackground? background);

    /// <summary>
    /// Turns the video denoiser on or off. Needs
    /// <c>Net.Agora.Extensions.ClearVision.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetVideoDenoiser(bool enabled);

    /// <summary>
    /// Turns low-light enhancement on or off. Needs
    /// <c>Net.Agora.Extensions.ClearVision.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <remarks>Both SDKs document enabling <see cref="SetVideoDenoiser"/> first.</remarks>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetLowLightEnhance(bool enabled);

    /// <summary>
    /// Turns colour enhancement on or off. Needs
    /// <c>Net.Agora.Extensions.ClearVision.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    void SetColorEnhance(bool enabled);

    /// <summary>
    /// Turns local face detection on or off. Needs
    /// <c>Net.Agora.Extensions.FaceDetection.Android</c> / <c>.iOS</c>.
    /// </summary>
    /// <inheritdoc cref="SetNoiseSuppression" path="/exception" />
    /// <remarks>
    /// Not available on macOS: the macOS engine does not implement the underlying selector even
    /// with the extension package referenced, so this always raises
    /// <see cref="AgoraVideoException"/> there. Android and iOS behave as described above.
    /// </remarks>
    void EnableFaceDetection(bool enabled);
}
