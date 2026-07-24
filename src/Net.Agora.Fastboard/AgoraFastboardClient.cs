namespace Net.Agora.Fastboard;

/// <summary>
/// Cross-platform <see cref="IAgoraFastboardClient"/>. The platform halves live in
/// Platforms/Android and Platforms/Apple; everything shared lives here.
/// </summary>
public sealed partial class AgoraFastboardClient : IAgoraFastboardClient
{
    private readonly AgoraFastboardOptions _options;

    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<AgoraFastboardPhaseEventArgs>? PhaseChanged;

    /// <inheritdoc />
    public event EventHandler<AgoraFastboardDisconnectedEventArgs>? Disconnected;

    /// <inheritdoc />
    public bool IsJoined { get; private set; }

    /// <inheritdoc />
    public async Task JoinAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();

        if (IsJoined)
        {
            throw new AgoraFastboardException("already in a room — disconnect before joining another.");
        }

        await AwaitOperation(JoinCore, "join the room", cancellationToken).ConfigureAwait(false);

        IsJoined = true;
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        if (!IsJoined)
        {
            return;
        }

        IsJoined = false;
        DisconnectCore();
    }

    /// <inheritdoc />
    public void SetTool(AgoraFastboardTool tool, uint? color = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireRoom();

        SetToolCore(tool, color);
    }

    /// <inheritdoc />
    public void Undo()
    {
        RequireRoom();
        UndoCore();
    }

    /// <inheritdoc />
    public void Redo()
    {
        RequireRoom();
        RedoCore();
    }

    /// <inheritdoc />
    public void Clear()
    {
        RequireRoom();
        ClearCore();
    }

    /// <inheritdoc />
    public Task SetWritableAsync(bool writable, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireRoom();

        return AwaitOperation(
            complete => SetWritableCore(writable, complete),
            writable ? "become writable" : "become read-only",
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsJoined = false;
        DisposeCore();
    }

    /// <summary>
    /// The appliance-name strings the whiteboard SDK underneath understands. Fastboard's Android
    /// side models these as a Java enum and its iOS side takes the raw string, so the string is
    /// what both halves can agree on.
    /// </summary>
    private static string ApplianceNameOf(AgoraFastboardTool tool) => tool switch
    {
        AgoraFastboardTool.Eraser => "eraser",
        AgoraFastboardTool.Selector => "selector",
        AgoraFastboardTool.Text => "text",
        AgoraFastboardTool.Rectangle => "rectangle",
        AgoraFastboardTool.Ellipse => "ellipse",
        AgoraFastboardTool.Straight => "straight",
        AgoraFastboardTool.Arrow => "arrow",
        AgoraFastboardTool.Hand => "hand",
        AgoraFastboardTool.LaserPointer => "laserPointer",
        AgoraFastboardTool.Clicker => "clicker",
        _ => "pencil",
    };

    private void RequireRoom()
    {
        if (!IsJoined)
        {
            throw new AgoraFastboardException("not in a room — call JoinAsync first.");
        }
    }

    /// <summary>
    /// Runs one native operation and awaits its callback, bounded by the configured timeout and the
    /// caller's token.
    /// </summary>
    private async Task AwaitOperation(
        Action<Action<AgoraFastboardException?>> start, string operation, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = new CancellationTokenSource(_options.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        await using var registration = linked.Token.Register(() =>
        {
            // Distinguishes the two reasons the token can fire: the caller's cancellation should
            // surface as OperationCanceledException, an expired timeout as a failure.
            if (cancellationToken.IsCancellationRequested)
            {
                pending.TrySetCanceled(cancellationToken);
            }
            else
            {
                pending.TrySetException(new AgoraFastboardException(
                    $"the board did not answer {operation} within {_options.Timeout.TotalSeconds:0}s."));
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

    private void RaisePhaseChanged(AgoraFastboardPhase phase)
    {
        if (phase == AgoraFastboardPhase.Disconnected)
        {
            IsJoined = false;
        }

        PhaseChanged?.Invoke(this, new AgoraFastboardPhaseEventArgs(phase));
    }

    private void RaiseDisconnected(string reason, bool kicked)
    {
        IsJoined = false;
        Disconnected?.Invoke(this, new AgoraFastboardDisconnectedEventArgs(reason, kicked));
    }
}
