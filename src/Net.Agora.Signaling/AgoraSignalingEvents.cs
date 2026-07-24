namespace Net.Agora.Signaling;

/// <summary>Raised when a signaling operation fails, or the SDK reports an error.</summary>
public sealed class AgoraSignalingException(string message, int errorCode) : Exception(message)
{
    /// <summary>The SDK's own error code — <c>AgoraRtmErrorCode</c> on iOS, <c>RtmConstants.RtmErrorCode</c> on Android.</summary>
    public int ErrorCode { get; } = errorCode;
}

/// <summary>Raised for <see cref="IAgoraSignalingClient.MessageReceived"/>.</summary>
public sealed class AgoraSignalingMessageEventArgs(
    string channelName, string publisher, string? text, byte[]? data) : EventArgs
{
    /// <summary>The channel the message was published to.</summary>
    public string ChannelName { get; } = channelName;

    /// <summary>The user ID of the publisher.</summary>
    public string Publisher { get; } = publisher;

    /// <summary>The payload, when the publisher sent a string. Exactly one of Text/Data is set.</summary>
    public string? Text { get; } = text;

    /// <summary>The payload, when the publisher sent raw bytes. Exactly one of Text/Data is set.</summary>
    public byte[]? Data { get; } = data;
}

/// <summary>The connection's lifecycle state — the same five states, with the same values, as the RTC products'.</summary>
public enum AgoraConnectionState
{
    /// <summary>Not logged in and not trying to reach the service.</summary>
    Disconnected = 1,

    /// <summary>A login is in flight.</summary>
    Connecting = 2,

    /// <summary>Logged in.</summary>
    Connected = 3,

    /// <summary>The SDK lost the service and is re-establishing it on its own.</summary>
    Reconnecting = 4,

    /// <summary>The SDK gave up; a fresh <c>LoginAsync</c> is required.</summary>
    Failed = 5,
}

/// <summary>Raised for <see cref="IAgoraSignalingClient.ConnectionStateChanged"/>.</summary>
public sealed class AgoraConnectionStateEventArgs(AgoraConnectionState state, int reason) : EventArgs
{
    /// <summary>The state the connection moved to.</summary>
    public AgoraConnectionState State { get; } = state;

    /// <summary>
    /// Why — the SDK's own reason code, carried raw: there are ~30 values and they matter mostly
    /// for logs.
    /// </summary>
    public int Reason { get; } = reason;
}
