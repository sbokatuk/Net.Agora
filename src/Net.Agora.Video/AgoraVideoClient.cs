namespace Net.Agora.Video;

/// <summary>
/// Cross-platform <see cref="IAgoraVideoClient"/>. The platform halves live in
/// Platforms/Android and Platforms/Apple; everything shared — events, state, and turning the
/// SDKs' callbacks into an awaitable <see cref="JoinAsync"/> — lives here.
/// </summary>
public sealed partial class AgoraVideoClient : IAgoraVideoClient
{
    private readonly AgoraVideoOptions _options;

    /// <summary>
    /// The in-flight <see cref="JoinAsync"/> call. Only one at a time: the SDK does not tell you
    /// which call a callback belongs to, so a second concurrent join could not be matched to its
    /// result — and Agora's engines only support being joined to one channel per instance anyway.
    /// </summary>
    private TaskCompletionSource<bool>? _pending;

    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<AgoraChannelEventArgs>? Joined;

    /// <inheritdoc />
    public event EventHandler<AgoraChannelEventArgs>? Left;

    /// <inheritdoc />
    public event EventHandler<AgoraUserEventArgs>? UserJoined;

    /// <inheritdoc />
    public event EventHandler<AgoraUserEventArgs>? UserOffline;

    /// <inheritdoc />
    public event EventHandler<AgoraRemoteAudioMuteEventArgs>? RemoteAudioMuted;

    /// <inheritdoc />
    public event EventHandler<AgoraRemoteVideoMuteEventArgs>? RemoteVideoMuted;

    /// <inheritdoc />
    public event EventHandler<AgoraVolumeIndicationEventArgs>? VolumeIndication;

    /// <inheritdoc />
    public event EventHandler<AgoraConnectionStateEventArgs>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler? TokenPrivilegeWillExpire;

    /// <inheritdoc />
    public event EventHandler<AgoraVideoErrorEventArgs>? Error;

    /// <inheritdoc />
    public bool IsJoined { get; private set; }

    /// <inheritdoc />
    public uint LocalUid { get; private set; }

    private string? _channelId;

    /// <inheritdoc />
    public async Task JoinAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        if (_pending is not null)
        {
            throw new InvalidOperationException(
                "a join is already in progress; await it or call Leave() first.");
        }

        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        _channelId = channelId;

        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await using var registration = linked.Token.Register(() =>
        {
            // Distinguishes the two reasons the token can fire: the caller's cancellation should
            // surface as OperationCanceledException, an expired timeout as a failure to join.
            if (cancellationToken.IsCancellationRequested)
            {
                pending.TrySetCanceled(cancellationToken);
            }
            else
            {
                pending.TrySetException(new AgoraVideoException(
                    $"the server did not confirm joining '{channelId}' within {_options.Timeout.TotalSeconds:0}s.",
                    errorCode: 0));
            }
        }).ConfigureAwait(false);

        try
        {
            JoinCore(channelId);
            await pending.Task.ConfigureAwait(false);
            IsJoined = true;
        }
        catch
        {
            _channelId = null;
            LeaveCore();
            throw;
        }
        finally
        {
            _pending = null;
        }
    }

    /// <inheritdoc />
    public void Leave()
    {
        LeaveCore();
        IsJoined = false;
        LocalUid = 0;
        _channelId = null;
    }

    /// <inheritdoc />
    public void EnableVolumeIndication(TimeSpan interval)
    {
        // Agora's floor: the engine answers -2 (invalid argument) to anything under 10 ms, and
        // its own documented minimum useful cadence is 200 ms. Validated here so the caller gets
        // an ArgumentOutOfRangeException naming the parameter rather than an SDK error code.
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        EnableVolumeIndicationCore((int)interval.TotalMilliseconds);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Leave();
        DisposeCore();
    }

    /// <summary>Called by the platform half when the server confirms the join.</summary>
    private void RaiseJoined(uint uid)
    {
        LocalUid = uid;
        _pending?.TrySetResult(true);
        Joined?.Invoke(this, new AgoraChannelEventArgs(_channelId ?? "", uid));
    }

    /// <summary>Called by the platform half when the channel is left.</summary>
    private void RaiseLeft()
    {
        var channelId = _channelId ?? "";
        var uid = LocalUid;
        IsJoined = false;
        Left?.Invoke(this, new AgoraChannelEventArgs(channelId, uid));
    }

    private void RaiseUserJoined(uint uid) => UserJoined?.Invoke(this, new AgoraUserEventArgs(uid));

    private void RaiseUserOffline(uint uid) => UserOffline?.Invoke(this, new AgoraUserEventArgs(uid));

    private void RaiseRemoteAudioMuted(uint uid, bool muted) =>
        RemoteAudioMuted?.Invoke(this, new AgoraRemoteAudioMuteEventArgs(uid, muted));

    private void RaiseRemoteVideoMuted(uint uid, bool muted) =>
        RemoteVideoMuted?.Invoke(this, new AgoraRemoteVideoMuteEventArgs(uid, muted));

    private void RaiseVolumeIndication(IReadOnlyList<AgoraSpeakerVolume> speakers, int totalVolume) =>
        VolumeIndication?.Invoke(this, new AgoraVolumeIndicationEventArgs(speakers, totalVolume));

    private void RaiseConnectionStateChanged(AgoraConnectionState state, int reason) =>
        ConnectionStateChanged?.Invoke(this, new AgoraConnectionStateEventArgs(state, reason));

    private void RaiseTokenPrivilegeWillExpire() => TokenPrivilegeWillExpire?.Invoke(this, EventArgs.Empty);

    /// <summary>Called by the platform half when the SDK reports an error.</summary>
    private void RaiseError(string message, int errorCode)
    {
        // An error while joining is JoinAsync's result, not just a notification.
        _pending?.TrySetException(new AgoraVideoException(message, errorCode));
        Error?.Invoke(this, new AgoraVideoErrorEventArgs(message, errorCode));
    }
}
