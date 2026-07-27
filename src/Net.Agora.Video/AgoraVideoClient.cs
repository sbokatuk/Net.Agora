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
        AgoraEngineSlot.Release();
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

    // ------------------------------------------------------------------------------------------
    // Extension controls
    // ------------------------------------------------------------------------------------------
    // Each of these is one native call whose return code is the only signal that the extension's
    // payload actually shipped: without the matching Net.Agora.Extensions.* package the engine
    // answers a failure code from a call that compiled and linked perfectly. So, unlike the rest
    // of this facade, the code is checked rather than ignored — a missing package is a deployment
    // mistake worth an exception, not a silently inert switch.

    /// <inheritdoc />
    public void SetNoiseSuppression(AgoraNoiseSuppression mode) =>
        Check(SetNoiseSuppressionCore(mode), nameof(SetNoiseSuppression), "Net.Agora.Extensions.Ains");

    /// <inheritdoc />
    public void SetVoiceBeautifier(AgoraVoiceBeautifier preset) =>
        Check(SetVoiceBeautifierCore(preset), nameof(SetVoiceBeautifier), "Net.Agora.Extensions.AudioBeauty");

    /// <inheritdoc />
    public void SetAudioEffect(AgoraAudioEffect preset) =>
        Check(SetAudioEffectCore(preset), nameof(SetAudioEffect), "Net.Agora.Extensions.AudioBeauty");

    /// <inheritdoc />
    public void SetVirtualBackground(AgoraVirtualBackground? background) =>
        Check(
            SetVirtualBackgroundCore(background),
            nameof(SetVirtualBackground),
            "Net.Agora.Extensions.VirtualBackground");

    /// <inheritdoc />
    public void SetVideoDenoiser(bool enabled) =>
        Check(SetVideoDenoiserCore(enabled), nameof(SetVideoDenoiser), "Net.Agora.Extensions.ClearVision");

    /// <inheritdoc />
    public void SetLowLightEnhance(bool enabled) =>
        Check(SetLowLightEnhanceCore(enabled), nameof(SetLowLightEnhance), "Net.Agora.Extensions.ClearVision");

    /// <inheritdoc />
    public void SetColorEnhance(bool enabled) =>
        Check(SetColorEnhanceCore(enabled), nameof(SetColorEnhance), "Net.Agora.Extensions.ClearVision");

    /// <inheritdoc />
    public void EnableFaceDetection(bool enabled) =>
        Check(EnableFaceDetectionCore(enabled), nameof(EnableFaceDetection), "Net.Agora.Extensions.FaceDetection");

    /// <summary>
    /// Turns a non-zero return from an extension switch into an exception that names the package
    /// most likely to be missing. Both SDKs answer -4 (not supported) or -157 (module not found)
    /// for an absent extension, but not consistently across versions, so the message covers the
    /// case rather than the code.
    /// </summary>
    private static void Check(int code, string operation, string package)
    {
        if (code == 0)
        {
            return;
        }

        throw new AgoraVideoException(
            $"{operation} was refused by the SDK (code {code}). If the extension's native payload " +
            $"is not in the app, add the {package}.Android / .iOS package for the platform you " +
            "are building.",
            code);
    }

    /// <summary>Called by the platform half when the SDK reports an error.</summary>
    private void RaiseError(string message, int errorCode)
    {
        // An error while joining is JoinAsync's result, not just a notification.
        _pending?.TrySetException(new AgoraVideoException(message, errorCode));
        Error?.Invoke(this, new AgoraVideoErrorEventArgs(message, errorCode));
    }
}
