namespace Net.Agora.Signaling;

/// <summary>
/// Cross-platform <see cref="IAgoraSignalingClient"/>. The platform halves live in
/// Platforms/Android and Platforms/Apple; everything shared — events, state, and turning the
/// SDKs’ per-operation callbacks into awaitable calls — lives here.
/// </summary>
public sealed partial class AgoraSignalingClient : IAgoraSignalingClient
{
    private readonly AgoraSignalingOptions _options;

    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<AgoraSignalingMessageEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler<AgoraConnectionStateEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler? TokenPrivilegeWillExpire;

    /// <inheritdoc />
    public bool IsLoggedIn { get; private set; }

    /// <inheritdoc />
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();

        // The token doubles as the App ID in projects with App ID-only authentication — the
        // convention Agora’s own SDKs document for RTM login.
        await AwaitOperation(
            complete => LoginCore(_options.Token ?? _options.AppId!, complete),
            "login", cancellationToken).ConfigureAwait(false);

        IsLoggedIn = true;
    }

    /// <inheritdoc />
    public void Logout()
    {
        LogoutCore();
        IsLoggedIn = false;
    }

    /// <inheritdoc />
    public Task SubscribeAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return AwaitOperation(
            complete => SubscribeCore(channelName, complete),
            $"subscribe '{channelName}'", cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string channelName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return AwaitOperation(
            complete => UnsubscribeCore(channelName, complete),
            $"unsubscribe '{channelName}'", cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishAsync(string channelName, string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ArgumentNullException.ThrowIfNull(message);

        return AwaitOperation(
            complete => PublishCore(channelName, message, complete),
            $"publish to '{channelName}'", cancellationToken);
    }

    /// <inheritdoc />
    public void RenewToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        RenewTokenCore(token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsLoggedIn = false;
        DisposeCore();
    }

    /// <summary>
    /// Runs one native operation and awaits its completion callback, bounded by the configured
    /// timeout and the caller’s token. Unlike the RTC clients’ single-join state
    /// machine, RTM reports every operation through its own callback, so concurrent operations
    /// are fine — each call gets its own completion.
    /// </summary>
    /// <param name="start">
    /// Starts the native call; the argument completes it — with null on success, or the failure
    /// mapped to an exception.
    /// </param>
    /// <param name="operation">Human-readable name for the timeout message.</param>
    /// <param name="cancellationToken">The caller's token — cancellation wins over the timeout.</param>
    private async Task AwaitOperation(
        Action<Action<Exception?>> start, string operation, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await using var registration = linked.Token.Register(() =>
        {
            // Distinguishes the two reasons the token can fire: the caller’s cancellation
            // should surface as OperationCanceledException, an expired timeout as a failure.
            if (cancellationToken.IsCancellationRequested)
            {
                pending.TrySetCanceled(cancellationToken);
            }
            else
            {
                pending.TrySetException(new AgoraSignalingException(
                    $"the service did not answer {operation} within {_options.Timeout.TotalSeconds:0}s.",
                    errorCode: 0));
            }
        }).ConfigureAwait(false);

        start(failure =>
        {
            if (failure is null)
            {
                pending.TrySetResult(true);
            }
            else
            {
                pending.TrySetException(failure);
            }
        });

        await pending.Task.ConfigureAwait(false);
    }

    private void RaiseMessageReceived(string channelName, string publisher, string? text, byte[]? data) =>
        MessageReceived?.Invoke(this, new AgoraSignalingMessageEventArgs(channelName, publisher, text, data));

    private void RaiseConnectionStateChanged(AgoraConnectionState state, int reason) =>
        ConnectionStateChanged?.Invoke(this, new AgoraConnectionStateEventArgs(state, reason));

    private void RaiseTokenPrivilegeWillExpire() => TokenPrivilegeWillExpire?.Invoke(this, EventArgs.Empty);
}
