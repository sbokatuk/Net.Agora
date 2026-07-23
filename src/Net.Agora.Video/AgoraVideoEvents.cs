namespace Net.Agora.Video;

/// <summary>Raised when joining a channel fails, or the SDK reports an error while joined.</summary>
public sealed class AgoraVideoException(string message, int errorCode) : Exception(message)
{
    /// <summary>The SDK's own error code — <c>AgoraErrorCode</c> on iOS, <c>Constants.ERR_*</c> on Android.</summary>
    public int ErrorCode { get; } = errorCode;
}

/// <summary>Raised for <see cref="IAgoraVideoClient.Joined"/> and <see cref="IAgoraVideoClient.Left"/>.</summary>
public sealed class AgoraChannelEventArgs(string channelId, uint uid) : EventArgs
{
    /// <summary>The channel this event concerns.</summary>
    public string ChannelId { get; } = channelId;

    /// <summary>The uid Agora assigned this client for the session.</summary>
    public uint Uid { get; } = uid;
}

/// <summary>Raised for <see cref="IAgoraVideoClient.UserJoined"/> and <see cref="IAgoraVideoClient.UserOffline"/>.</summary>
public sealed class AgoraUserEventArgs(uint uid) : EventArgs
{
    /// <summary>The remote user's uid — pass to <c>SetRemoteView</c> to render their video.</summary>
    public uint Uid { get; } = uid;
}

/// <summary>Raised for <see cref="IAgoraVideoClient.Error"/>.</summary>
public sealed class AgoraVideoErrorEventArgs(string message, int errorCode) : EventArgs
{
    /// <summary>A human-readable description of the error.</summary>
    public string Message { get; } = message;

    /// <summary>The SDK's own error code — <c>AgoraErrorCode</c> on iOS, <c>Constants.ERR_*</c> on Android.</summary>
    public int ErrorCode { get; } = errorCode;
}
