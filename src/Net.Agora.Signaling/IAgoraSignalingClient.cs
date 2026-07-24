namespace Net.Agora.Signaling;

/// <summary>
/// Logs in to Agora Signaling (RTM), subscribes to message channels and publishes/receives
/// messages, with the same surface on Android and iOS.
///
/// The platform bindings underneath do not resemble each other — Android’s <c>RtmClient</c>
/// reports every operation through a Java <c>ResultCallback</c>, iOS’s
/// <c>AgoraRtmClientKit</c> through Objective-C completion blocks — and this is the layer that
/// hides the difference behind ordinary awaitable calls. Reach for <c>Agora.Rtm.*</c> (Android,
/// via Net.Agora.Signaling.Android) or <c>Net.Agora.Signaling.iOS.*</c> directly for anything not
/// exposed here (stream channels and topics, presence, storage, locks, history).
///
/// Signaling coexists with either RTC product in one app — different native libraries, different
/// classes.
/// </summary>
public interface IAgoraSignalingClient : IDisposable
{
    /// <summary>A message arrived on a subscribed channel.</summary>
    event EventHandler<AgoraSignalingMessageEventArgs>? MessageReceived;

    /// <summary>
    /// The connection’s lifecycle: connecting, connected, reconnecting after a drop, failed.
    /// The SDK reconnects on its own — <see cref="AgoraConnectionState.Reconnecting"/> is
    /// informational, <see cref="AgoraConnectionState.Failed"/> is when a fresh
    /// <see cref="LoginAsync"/> is needed.
    /// </summary>
    event EventHandler<AgoraConnectionStateEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// The token is about to expire — obtain a fresh one from your token server and pass it to
    /// <see cref="RenewToken"/>, or the client will be disconnected when it lapses.
    /// </summary>
    event EventHandler? TokenPrivilegeWillExpire;

    /// <summary>True between a successful login and <see cref="Logout"/> or a drop.</summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// Logs in to the Signaling service, completing when the service confirms.
    /// </summary>
    /// <exception cref="AgoraSignalingException">
    /// The SDK reported an error, or did not confirm within <see cref="AgoraSignalingOptions.Timeout"/>.
    /// </exception>
    Task LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>Logs out. Safe to call when idle.</summary>
    void Logout();

    /// <summary>
    /// Subscribes to a message channel, completing when the service confirms —
    /// <see cref="MessageReceived"/> fires for it from then on.
    /// </summary>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task SubscribeAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Unsubscribes from a message channel, completing when the service confirms.</summary>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task UnsubscribeAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a string message to a channel, completing when the service accepts it.
    /// Subscribers receive it through <see cref="MessageReceived"/>; the publisher itself does not.
    /// </summary>
    /// <inheritdoc cref="LoginAsync" path="/exception" />
    Task PublishAsync(string channelName, string message, CancellationToken cancellationToken = default);

    /// <summary>Replaces the token — see <see cref="TokenPrivilegeWillExpire"/>.</summary>
    void RenewToken(string token);
}
