namespace Net.Agora.Voice;

/// <summary>Raised when joining a channel fails, or the SDK reports an error while joined.</summary>
public sealed class AgoraVoiceException : Exception
{
    /// <summary>Creates the exception for an SDK failure that reported <paramref name="errorCode"/>.</summary>
    public AgoraVoiceException(string message, int errorCode)
        : base(message) => ErrorCode = errorCode;

    /// <summary>
    /// As above, keeping the platform exception that caused it — used where the failure arrives
    /// as a native exception rather than a code, so the original stack is not thrown away.
    /// </summary>
    public AgoraVoiceException(string message, int errorCode, Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;

    /// <summary>The SDK's own error code — <c>AgoraErrorCode</c> on iOS, <c>Constants.ERR_*</c> on Android.</summary>
    public int ErrorCode { get; }
}

/// <summary>Raised for <see cref="IAgoraVoiceClient.Joined"/> and <see cref="IAgoraVoiceClient.Left"/>.</summary>
public sealed class AgoraChannelEventArgs(string channelId, uint uid) : EventArgs
{
    /// <summary>The channel this event concerns.</summary>
    public string ChannelId { get; } = channelId;

    /// <summary>The uid Agora assigned this client for the session.</summary>
    public uint Uid { get; } = uid;
}

/// <summary>Raised for <see cref="IAgoraVoiceClient.UserJoined"/> and <see cref="IAgoraVoiceClient.UserOffline"/>.</summary>
public sealed class AgoraUserEventArgs(uint uid) : EventArgs
{
    /// <summary>The remote user's uid.</summary>
    public uint Uid { get; } = uid;
}

/// <summary>Raised for <see cref="IAgoraVoiceClient.RemoteAudioMuted"/>.</summary>
public sealed class AgoraRemoteAudioMuteEventArgs(uint uid, bool muted) : EventArgs
{
    /// <summary>The remote user who muted or unmuted.</summary>
    public uint Uid { get; } = uid;

    /// <summary>True when they muted, false when they unmuted.</summary>
    public bool Muted { get; } = muted;
}

/// <summary>One speaker's entry in a <see cref="AgoraVolumeIndicationEventArgs"/> report.</summary>
/// <param name="Uid">The speaker's uid — 0 means this client in the local report.</param>
/// <param name="Volume">0 (silent) to 255 (loudest).</param>
public readonly record struct AgoraSpeakerVolume(uint Uid, int Volume);

/// <summary>Raised for <see cref="IAgoraVoiceClient.VolumeIndication"/>.</summary>
public sealed class AgoraVolumeIndicationEventArgs(
    IReadOnlyList<AgoraSpeakerVolume> speakers, int totalVolume) : EventArgs
{
    /// <summary>The loudest few speakers right now; empty when everyone is silent.</summary>
    public IReadOnlyList<AgoraSpeakerVolume> Speakers { get; } = speakers;

    /// <summary>The mixed volume of everyone, 0–255.</summary>
    public int TotalVolume { get; } = totalVolume;
}

/// <summary>The connection's lifecycle state — Agora's <c>AgoraConnectionState</c> / <c>CONNECTION_STATE_*</c>.</summary>
public enum AgoraConnectionState
{
    /// <summary>No channel and not trying to reach one.</summary>
    Disconnected = 1,

    /// <summary>A join is in flight.</summary>
    Connecting = 2,

    /// <summary>In the channel.</summary>
    Connected = 3,

    /// <summary>The SDK lost the channel and is re-establishing it on its own.</summary>
    Reconnecting = 4,

    /// <summary>The SDK gave up; a fresh <c>JoinAsync</c> is required.</summary>
    Failed = 5,
}

/// <summary>Raised for <see cref="IAgoraVoiceClient.ConnectionStateChanged"/>.</summary>
public sealed class AgoraConnectionStateEventArgs(AgoraConnectionState state, int reason) : EventArgs
{
    /// <summary>The state the connection moved to.</summary>
    public AgoraConnectionState State { get; } = state;

    /// <summary>
    /// Why — the SDK's own <c>CONNECTION_CHANGED_*</c> / <c>AgoraConnectionChangedReason</c> code,
    /// carried raw: there are ~30 values and they matter mostly for logs.
    /// </summary>
    public int Reason { get; } = reason;
}

/// <summary>Raised for <see cref="IAgoraVoiceClient.Error"/>.</summary>
public sealed class AgoraVoiceErrorEventArgs(string message, int errorCode) : EventArgs
{
    /// <summary>A human-readable description of the error.</summary>
    public string Message { get; } = message;

    /// <summary>The SDK's own error code — <c>AgoraErrorCode</c> on iOS, <c>Constants.ERR_*</c> on Android.</summary>
    public int ErrorCode { get; } = errorCode;
}
