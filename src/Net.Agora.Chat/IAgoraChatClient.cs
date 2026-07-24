namespace Net.Agora.Chat;

/// <summary>
/// Signs in to Agora Chat, sends and receives text messages and reads the conversation list, with
/// the same surface on Android and iOS.
///
/// The platform bindings underneath do not resemble each other — Android’s <c>ChatClient</c>
/// reports sign-in through a Java <c>CallBack</c> and a send through a callback attached to the
/// message itself, iOS’s <c>AgoraChatClient</c> reports both through Objective-C completion
/// blocks — and this is the layer that hides the difference behind ordinary awaitable calls.
/// Reach for <c>Agora.Chat.*</c> (Android, via Net.Agora.Chat.Android) or
/// <c>Net.Agora.Chat.iOS.*</c> directly for anything not exposed here: groups, chat rooms,
/// threads, presence, contacts, reactions, translation, push, and the non-text message bodies.
///
/// Chat coexists with any of the RTC or Signaling products in one app — different native
/// libraries, different classes.
/// </summary>
public interface IAgoraChatClient : IDisposable
{
    /// <summary>
    /// A message arrived. Only text messages are surfaced with their content; other body types
    /// arrive with <see cref="AgoraChatMessage.Text"/> null.
    /// </summary>
    event EventHandler<AgoraChatMessageEventArgs>? MessageReceived;

    /// <summary>
    /// The connection came up or went down. The SDK reconnects on its own, so a
    /// <see cref="AgoraChatConnectionState.Disconnected"/> is informational unless
    /// <see cref="ForcedLogout"/> follows it.
    /// </summary>
    event EventHandler<AgoraChatConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// The SDK ended the session without being asked to. A fresh <see cref="LoginAsync"/> — with a
    /// new token, if the cause was expiry — is needed to carry on.
    /// </summary>
    event EventHandler<AgoraChatForcedLogoutEventArgs>? ForcedLogout;

    /// <summary>
    /// The token is about to expire — obtain a fresh one from your token server and pass it to
    /// <see cref="RenewTokenAsync"/>, or the session will end when it lapses.
    /// </summary>
    event EventHandler? TokenPrivilegeWillExpire;

    /// <summary>True between a successful sign-in and <see cref="LogoutAsync"/> or a forced logout.</summary>
    bool IsLoggedIn { get; }

    /// <summary>The signed-in user ID, or null when signed out.</summary>
    string? CurrentUserId { get; }

    /// <summary>
    /// Signs in to the Chat service, completing when the service confirms.
    /// </summary>
    /// <exception cref="AgoraChatException">
    /// The SDK reported an error, or did not confirm within <see cref="AgoraChatOptions.Timeout"/>.
    /// </exception>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Signs out, completing when the SDK has torn the session down. Safe to call when idle.</summary>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a text message to a user, group or room, completing when the service accepts it. The
    /// returned message carries the server-assigned <see cref="AgoraChatMessage.MessageId"/> and
    /// timestamp.
    /// </summary>
    /// <param name="conversationId">The peer's user ID, the group ID or the room ID.</param>
    /// <param name="text">The message body.</param>
    /// <param name="chatType">Which of those three <paramref name="conversationId"/> names.</param>
    /// <param name="cancellationToken">Cancels the wait, not the send: the SDK has no way to recall an in-flight message.</param>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task<AgoraChatMessage> SendTextMessageAsync(
        string conversationId,
        string text,
        AgoraChatType chatType = AgoraChatType.Chat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The conversations in the SDK's local database, most recent first.
    ///
    /// Local only — this does not reach the server, so it is empty on a fresh install until
    /// messages have arrived.
    /// </summary>
    IReadOnlyList<AgoraChatConversation> GetConversations();

    /// <summary>Replaces the token — see <see cref="TokenPrivilegeWillExpire"/>.</summary>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task RenewTokenAsync(string token, CancellationToken cancellationToken = default);
}
