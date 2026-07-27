namespace Net.Agora.Video;

/// <summary>
/// The neutral-target-framework half of <see cref="AgoraVideoClient"/> — the counterpart of
/// Platforms/Android and Platforms/Apple, compiled into the plain net8.0/net9.0/net10.0 build.
/// There is no engine on these target frameworks; the constructor throws, and it is the only
/// reachable member.
/// </summary>
/// <remarks>
/// The leg exists so shared code — ViewModels, tests, heads for platforms Agora does not ship on
/// — can reference the package and program against <see cref="IAgoraVideoClient"/> instead of
/// failing restore with NU1202. Construct the real client in a net*-android / net*-ios /
/// net*-macos application (directly, or through Net.Agora.Video.Maui's <c>CreateClient</c>) and
/// inject it. SetLocalView/SetRemoteView do not exist here: their view parameter is a platform
/// type, so code that renders video is platform code by definition.
/// </remarks>
public sealed partial class AgoraVideoClient
{
    /// <summary>Always throws — see the class remarks for where to construct the real client.</summary>
    /// <exception cref="PlatformNotSupportedException">Always.</exception>
    public AgoraVideoClient(AgoraVideoOptions options)
    {
        // Assigned rather than discarded only so the shared half's field is assigned somewhere in
        // this compilation (CS0649); the throw makes the value unreachable.
        _options = options;
        throw NotSupported();
    }

    private static PlatformNotSupportedException NotSupported() => new(
        "Net.Agora.Video has no engine on this target framework — the neutral build exists so " +
        "shared code can reference the package and program against IAgoraVideoClient. Construct " +
        "the client in a net*-android, net*-ios or net*-macos application head and inject it.");

    // Everything below satisfies the shared half of the class. The constructor is the only
    // entry point and always throws, so none of it can run.

    /// <inheritdoc cref="IAgoraVideoClient.EnableVideo" />
    public void EnableVideo() => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.DisableVideo" />
    public void DisableVideo() => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalAudio" />
    public void MuteLocalAudio(bool mute) => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.MuteLocalVideo" />
    public void MuteLocalVideo(bool mute) => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.StartPreview" />
    public void StartPreview() => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.StopPreview" />
    public void StopPreview() => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.SwitchCamera" />
    public void SwitchCamera() => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.SetSpeakerphone" />
    public void SetSpeakerphone(bool speakerphone) => throw NotSupported();

    /// <inheritdoc cref="IAgoraVideoClient.RenewToken" />
    public void RenewToken(string token) => throw NotSupported();

    private void EnableVolumeIndicationCore(int intervalMilliseconds) => throw NotSupported();

    private void JoinCore(string channelId) => throw NotSupported();

    private void LeaveCore() => throw NotSupported();

    private void DisposeCore()
    {
        // Nothing to release: construction always throws, so no instance ever holds resources.
    }

    private int SetNoiseSuppressionCore(AgoraNoiseSuppression mode) => throw NotSupported();

    private int SetVoiceBeautifierCore(AgoraVoiceBeautifier preset) => throw NotSupported();

    private int SetAudioEffectCore(AgoraAudioEffect preset) => throw NotSupported();

    private int SetVirtualBackgroundCore(AgoraVirtualBackground? background) => throw NotSupported();

    private int SetVideoDenoiserCore(bool enabled) => throw NotSupported();

    private int SetLowLightEnhanceCore(bool enabled) => throw NotSupported();

    private int SetColorEnhanceCore(bool enabled) => throw NotSupported();

    private int EnableFaceDetectionCore(bool enabled) => throw NotSupported();
}
