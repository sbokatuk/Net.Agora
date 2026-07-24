namespace Net.Agora.Chat;

/// <summary>Raised when a chat operation fails, or the SDK reports an error.</summary>
public sealed class AgoraChatException(string message, int errorCode) : Exception(message)
{
    /// <summary>
    /// The SDK's own error code — <c>AgoraChatErrorCode</c> on iOS, <c>Error</c> on Android. The
    /// two enumerations share their values.
    /// </summary>
    public int ErrorCode { get; } = errorCode;
}

/// <summary>Which kind of conversation a message belongs to.</summary>
public enum AgoraChatType
{
    /// <summary>A one-to-one conversation with another user.</summary>
    Chat = 0,

    /// <summary>A persistent group.</summary>
    GroupChat = 1,

    /// <summary>An ephemeral chat room.</summary>
    ChatRoom = 2,
}

/// <summary>
/// One chat message, flattened to the fields both SDKs agree on. Only text messages are surfaced:
/// a message whose body is an image, file, voice, video, location, command or custom payload
/// arrives with <see cref="Text"/> null. Reach for the platform bindings for those.
/// </summary>
public sealed class AgoraChatMessage(
    string messageId,
    string conversationId,
    string from,
    string to,
    string? text,
    AgoraChatType chatType,
    DateTimeOffset timestamp)
{
    /// <summary>The server-assigned ID. Empty until the service accepts a message being sent.</summary>
    public string MessageId { get; } = messageId;

    /// <summary>
    /// The conversation this belongs to — the peer's user ID, the group ID or the room ID,
    /// depending on <see cref="ChatType"/>.
    /// </summary>
    public string ConversationId { get; } = conversationId;

    /// <summary>The sender's user ID.</summary>
    public string From { get; } = from;

    /// <summary>The recipient — a user ID, group ID or room ID.</summary>
    public string To { get; } = to;

    /// <summary>The text, or null when the message carries some other kind of body.</summary>
    public string? Text { get; } = text;

    /// <summary>Which kind of conversation this belongs to.</summary>
    public AgoraChatType ChatType { get; } = chatType;

    /// <summary>When the service received it.</summary>
    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>One conversation, as the SDK's local database has it.</summary>
public sealed class AgoraChatConversation(
    string conversationId, AgoraChatType type, int unreadCount, AgoraChatMessage? latestMessage)
{
    /// <summary>The peer's user ID, the group ID or the room ID.</summary>
    public string ConversationId { get; } = conversationId;

    /// <summary>Which kind of conversation this is.</summary>
    public AgoraChatType Type { get; } = type;

    /// <summary>How many messages in it have not been marked read.</summary>
    public int UnreadCount { get; } = unreadCount;

    /// <summary>The most recent message, or null for an empty conversation.</summary>
    public AgoraChatMessage? LatestMessage { get; } = latestMessage;
}

/// <summary>Raised for <see cref="IAgoraChatClient.MessageReceived"/>.</summary>
public sealed class AgoraChatMessageEventArgs(AgoraChatMessage message) : EventArgs
{
    /// <summary>The message that arrived.</summary>
    public AgoraChatMessage Message { get; } = message;
}

/// <summary>
/// Whether the SDK currently has a connection to the Chat service. Two states, not the five the
/// RTC and Signaling products report: Chat's own callback distinguishes only connected from
/// disconnected, and reports why a sign-in failed through the sign-in call instead.
/// </summary>
public enum AgoraChatConnectionState
{
    /// <summary>No connection. The SDK reconnects on its own unless the session was ended.</summary>
    Disconnected = 0,

    /// <summary>Connected and able to send.</summary>
    Connected = 1,
}

/// <summary>Raised for <see cref="IAgoraChatClient.ConnectionStateChanged"/>.</summary>
public sealed class AgoraChatConnectionStateEventArgs(AgoraChatConnectionState state) : EventArgs
{
    /// <summary>The state the connection moved to.</summary>
    public AgoraChatConnectionState State { get; } = state;
}

/// <summary>Why the SDK ended the session without being asked to.</summary>
public enum AgoraChatLogoutReason
{
    /// <summary>The same user ID signed in from another device.</summary>
    LoggedInElsewhere = 0,

    /// <summary>The account was deleted on the server.</summary>
    AccountRemoved = 1,

    /// <summary>The token expired and was not renewed in time.</summary>
    TokenExpired = 2,

    /// <summary>Something else — the SDKs report a few rarer causes that do not map across both.</summary>
    Other = 3,
}

/// <summary>Raised for <see cref="IAgoraChatClient.ForcedLogout"/>.</summary>
public sealed class AgoraChatForcedLogoutEventArgs(AgoraChatLogoutReason reason) : EventArgs
{
    /// <summary>Why the session ended.</summary>
    public AgoraChatLogoutReason Reason { get; } = reason;
}
