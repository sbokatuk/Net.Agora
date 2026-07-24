using Foundation;
using Net.Agora.Fastboard.iOS;

using NativeFastboard = Net.Agora.Fastboard.iOS.AgoraFastboard;
using NativePhase = Net.Agora.Fastboard.iOS.AgoraFastboardPhase;

namespace Net.Agora.Fastboard;

public sealed partial class AgoraFastboardClient
{
    private readonly NativeFastboard _fastboard;
    private readonly BoardDelegate _delegate;

    private Action<AgoraFastboardException?>? _pendingJoin;

    /// <summary>
    /// Creates the board and joins nothing yet — call <see cref="IAgoraFastboardClient.JoinAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraFastboardOptions.Validate"/> for what is required.</param>
    public AgoraFastboardClient(AgoraFastboardOptions options)
    {
        options.Validate();
        _options = options;

        var configuration = new AgoraFastboardConfiguration(
            options.AppIdentifier!, options.RoomUuid!, options.RoomToken!, options.Uid!)
        {
            // The shim maps the key onto Fastboard's own Region enum; empty means China, its
            // default.
            Region = options.Region ?? string.Empty,
        };

        _fastboard = new NativeFastboard(configuration);
        _delegate = new BoardDelegate(this);
        _fastboard.Delegate = _delegate;
    }

    /// <summary>
    /// The board and its toolbar. Unlike Android, where the view is passed in, the iOS shim creates
    /// it — Fastboard owns the view on that platform. The MAUI companion package puts it in a MAUI
    /// layout for you.
    /// </summary>
    public UIKit.UIView View => _fastboard.View;

    private void JoinCore(Action<AgoraFastboardException?> complete)
    {
        // The shim's join is fire-and-forget: Fastboard's own completion-handler overload takes a
        // Swift Result, which cannot cross into Objective-C, so the outcome arrives on the
        // delegate — the same shape as Android's listener.
        _pendingJoin = complete;
        _fastboard.Join();
    }

    private void DisconnectCore() => _fastboard.Disconnect();

    private void SetToolCore(AgoraFastboardTool tool, uint? color)
    {
        NSNumber[]? components = color is { } rgb
            ?
            [
                NSNumber.FromInt32((int)((rgb >> 16) & 0xFF)),
                NSNumber.FromInt32((int)((rgb >> 8) & 0xFF)),
                NSNumber.FromInt32((int)(rgb & 0xFF)),
            ]
            : null;

        _fastboard.SetAppliance(ApplianceNameOf(tool), components, null);
    }

    private void UndoCore() => _fastboard.Undo();

    private void RedoCore() => _fastboard.Redo();

    private void ClearCore() => _fastboard.Clear(true);

    private void SetWritableCore(bool writable, Action<AgoraFastboardException?> complete) =>
        _fastboard.SetWritable(writable, message =>
            complete(message is null ? null : new AgoraFastboardException(message)));

    private void DisposeCore() => _fastboard.Disconnect();

    /// <summary>
    /// Translates the shim's delegate into the shared partial's Raise* calls, and completes a
    /// pending join.
    /// </summary>
    private sealed class BoardDelegate(AgoraFastboardClient owner) : AgoraFastboardDelegate
    {
        public override void FastboardDidJoin()
        {
            var pending = owner._pendingJoin;
            owner._pendingJoin = null;
            pending?.Invoke(null);
        }

        public override void FastboardDidFail(string message)
        {
            var pending = owner._pendingJoin;
            if (pending is not null)
            {
                owner._pendingJoin = null;
                pending(new AgoraFastboardException(message));
                return;
            }

            owner.RaiseDisconnected(message, kicked: false);
        }

        public override void FastboardWasKicked(string reason) =>
            owner.RaiseDisconnected(reason, kicked: true);

        public override void FastboardPhaseDidChange(NativePhase phase)
        {
            // The shim carries Fastboard's sixth "unknown" case, which the facade's five-state
            // enum has no room for; reported as Disconnected, which is what it means in practice.
            if (phase == NativePhase.Unknown)
            {
                owner.RaisePhaseChanged(AgoraFastboardPhase.Disconnected);
                return;
            }

            owner.RaisePhaseChanged((AgoraFastboardPhase)(long)phase);
        }
    }
}
