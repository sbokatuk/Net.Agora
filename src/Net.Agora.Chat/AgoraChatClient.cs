namespace Net.Agora.Chat;

/// <summary>
/// Cross-platform <see cref="IAgoraChatClient"/>. The platform halves live in Platforms/Android
/// and Platforms/Apple; everything shared — events, state, and turning the SDKs’ per-operation
/// callbacks into awaitable calls — lives here.
/// </summary>
public sealed partial class AgoraChatClient : IAgoraChatClient
{
    private readonly AgoraChatOptions _options;

    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<AgoraChatMessageEventArgs>? MessageReceived;

    /// <inheritdoc />
    public event EventHandler<AgoraChatConnectionStateEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<AgoraChatForcedLogoutEventArgs>? ForcedLogout;

    /// <inheritdoc />
    public event EventHandler? TokenPrivilegeWillExpire;

    /// <inheritdoc />
    public bool IsLoggedIn { get; private set; }

    /// <inheritdoc />
    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();

        await AwaitOperation(
            complete => LoginCore(_options.UserId!, _options.Token!, complete),
            "sign in", cancellationToken).ConfigureAwait(false);

        IsLoggedIn = true;
    }

    /// <inheritdoc />
    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsLoggedIn)
        {
            return;
        }

        // false: unbinding the device token only matters to an app that registered for push, and
        // this façade does not expose push registration.
        await AwaitOperation(
            complete => LogoutCore(unbindDeviceToken: false, complete),
            "sign out", cancellationToken).ConfigureAwait(false);

        IsLoggedIn = false;
    }

    /// <inheritdoc />
    public Task<AgoraChatMessage> SendTextMessageAsync(
        string conversationId,
        string text,
        AgoraChatType chatType = AgoraChatType.Chat,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(text);

        return AwaitOperation<AgoraChatMessage>(
            complete => SendTextMessageCore(conversationId, text, chatType, complete),
            $"send to '{conversationId}'", cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgoraChatConversation> GetConversations()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetConversationsCore();
    }

    /// <inheritdoc />
    public Task RenewTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return AwaitOperation(
            complete => RenewTokenCore(token, complete), "renew the token", cancellationToken);
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
    /// Runs one native operation that produces no value and awaits its completion callback.
    /// </summary>
    private Task AwaitOperation(
        Action<Action<AgoraChatException?>> start, string operation, CancellationToken cancellationToken) =>
        AwaitOperation<bool>(
            complete => start(failure => complete(true, failure)), operation, cancellationToken);

    /// <summary>
    /// Runs one native operation and awaits its completion callback, bounded by the configured
    /// timeout and the caller’s token. Like Signaling and unlike the RTC clients’ single-join
    /// state machine, Chat reports every operation through its own callback, so concurrent
    /// operations are fine — each call gets its own completion.
    /// </summary>
    /// <param name="start">
    /// Starts the native call; the argument completes it — with the result and null on success, or
    /// with the failure mapped to an exception.
    /// </param>
    /// <param name="operation">Human-readable name for the timeout message.</param>
    /// <param name="cancellationToken">The caller's token — cancellation wins over the timeout.</param>
    private async Task<T> AwaitOperation<T>(
        Action<Action<T, AgoraChatException?>> start, string operation, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await using var registration = linked.Token.Register(() =>
        {
            // Distinguishes the two reasons the token can fire: the caller’s cancellation should
            // surface as OperationCanceledException, an expired timeout as a failure.
            if (cancellationToken.IsCancellationRequested)
            {
                pending.TrySetCanceled(cancellationToken);
            }
            else
            {
                pending.TrySetException(new AgoraChatException(
                    $"the service did not answer {operation} within {_options.Timeout.TotalSeconds:0}s.",
                    errorCode: 0));
            }
        }).ConfigureAwait(false);

        start((result, failure) =>
        {
            if (failure is null)
            {
                pending.TrySetResult(result);
            }
            else
            {
                pending.TrySetException(failure);
            }
        });

        return await pending.Task.ConfigureAwait(false);
    }

    private void RaiseMessageReceived(AgoraChatMessage message) =>
        MessageReceived?.Invoke(this, new AgoraChatMessageEventArgs(message));

    private void RaiseConnectionStateChanged(AgoraChatConnectionState state) =>
        ConnectionStateChanged?.Invoke(this, new AgoraChatConnectionStateEventArgs(state));

    private void RaiseForcedLogout(AgoraChatLogoutReason reason)
    {
        IsLoggedIn = false;
        ForcedLogout?.Invoke(this, new AgoraChatForcedLogoutEventArgs(reason));
    }

    private void RaiseTokenPrivilegeWillExpire() => TokenPrivilegeWillExpire?.Invoke(this, EventArgs.Empty);
}
