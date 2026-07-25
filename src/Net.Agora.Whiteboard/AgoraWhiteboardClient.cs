namespace Net.Agora.Whiteboard;

/// <summary>
/// Cross-platform <see cref="IAgoraWhiteboardClient"/>. The platform halves live in
/// Platforms/Android and Platforms/Apple; everything shared — events, state, and turning the SDKs'
/// join callbacks into an awaitable call — lives here.
/// </summary>
public sealed partial class AgoraWhiteboardClient : IAgoraWhiteboardClient
{
    private readonly AgoraWhiteboardOptions _options;

    private bool _disposed;
    private int _undoSteps;
    private int _redoSteps;

    /// <inheritdoc />
    public event EventHandler<AgoraWhiteboardPhaseEventArgs>? PhaseChanged;

    /// <inheritdoc />
    public event EventHandler<AgoraWhiteboardDisconnectedEventArgs>? Disconnected;

    /// <inheritdoc />
    public event EventHandler<AgoraWhiteboardHistoryEventArgs>? HistoryChanged;

    /// <inheritdoc />
    public bool IsJoined { get; private set; }

    /// <inheritdoc />
    public async Task JoinAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();

        if (IsJoined)
        {
            throw new AgoraWhiteboardException("already in a room — disconnect before joining another.");
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
    public void SetTool(AgoraWhiteboardTool tool, uint? color = null, double? strokeWidth = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireRoom();

        SetToolCore(ApplianceNameOf(tool), color is { } rgb ? ToRgbComponents(rgb) : null, strokeWidth);
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
    public void Clear(bool retainDocument = true)
    {
        RequireRoom();
        ClearCore(retainDocument);
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
    public void RefreshViewSize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RefreshViewSizeCore();
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
    /// The SDKs' own appliance-name strings. They are the same on both platforms because both are
    /// passing them straight to the board's JavaScript, which is the only thing that reads them.
    /// </summary>
    private static string ApplianceNameOf(AgoraWhiteboardTool tool) => tool switch
    {
        AgoraWhiteboardTool.Eraser => "eraser",
        AgoraWhiteboardTool.Selector => "selector",
        AgoraWhiteboardTool.Text => "text",
        AgoraWhiteboardTool.Rectangle => "rectangle",
        AgoraWhiteboardTool.Ellipse => "ellipse",
        AgoraWhiteboardTool.Straight => "straight",
        AgoraWhiteboardTool.Arrow => "arrow",
        AgoraWhiteboardTool.Hand => "hand",
        AgoraWhiteboardTool.LaserPointer => "laserPointer",
        AgoraWhiteboardTool.Clicker => "clicker",
        _ => "pencil",
    };

    /// <summary>
    /// Splits 0xRRGGBB into the three-element 0..255 array both SDKs take. Neither accepts a packed
    /// integer, and both reject a wrong-length array silently by ignoring the whole state change.
    /// </summary>
    private static int[] ToRgbComponents(uint rgb) =>
        [(int)((rgb >> 16) & 0xFF), (int)((rgb >> 8) & 0xFF), (int)(rgb & 0xFF)];

    private void RequireRoom()
    {
        if (!IsJoined)
        {
            throw new AgoraWhiteboardException("not in a room — call JoinAsync first.");
        }
    }

    /// <summary>
    /// Runs one native operation and awaits its callback, bounded by the configured timeout and the
    /// caller's token.
    /// </summary>
    private async Task AwaitOperation(
        Action<Action<AgoraWhiteboardException?>> start, string operation, CancellationToken cancellationToken)
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
                pending.TrySetException(new AgoraWhiteboardException(
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

    private void RaisePhaseChanged(AgoraWhiteboardPhase phase)
    {
        if (phase == AgoraWhiteboardPhase.Disconnected)
        {
            IsJoined = false;
        }

        PhaseChanged?.Invoke(this, new AgoraWhiteboardPhaseEventArgs(phase));
    }

    private void RaiseDisconnected(string reason, bool kicked)
    {
        IsJoined = false;
        Disconnected?.Invoke(this, new AgoraWhiteboardDisconnectedEventArgs(reason, kicked));
    }

    // The two step counts arrive on separate callbacks on both platforms, so each is remembered and
    // the pair reported — a UI wants both to decide what to enable.
    private void RaiseUndoSteps(int steps)
    {
        _undoSteps = steps;
        HistoryChanged?.Invoke(this, new AgoraWhiteboardHistoryEventArgs(_undoSteps, _redoSteps));
    }

    private void RaiseRedoSteps(int steps)
    {
        _redoSteps = steps;
        HistoryChanged?.Invoke(this, new AgoraWhiteboardHistoryEventArgs(_undoSteps, _redoSteps));
    }
}
