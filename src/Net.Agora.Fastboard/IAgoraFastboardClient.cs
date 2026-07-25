namespace Net.Agora.Fastboard;

/// <summary>
/// Joins an Agora Interactive Whiteboard room through Fastboard — netless's ready-made UI over the
/// board — with the same surface on Android and iOS.
///
/// The difference from <c>Net.Agora.Whiteboard</c> is what comes with the view: Fastboard draws a
/// working toolbar (tools, colours, undo/redo, page navigation) and wires it to the board, where
/// the plain whiteboard client hands you a bare canvas to build a UI around. The API below is what
/// is left once the toolbar handles the rest — the calls an app makes to drive the board itself.
///
/// The platforms diverge more here than anywhere else in this repository, and it is hidden
/// entirely. Android's Fastboard is Java and reports a join through a listener; iOS's is *Swift*,
/// whose <c>@objc</c> classes register under mangled runtime names, so what
/// Net.Agora.Fastboard.iOS binds is a purpose-built Objective-C shim rather than the SDK. See
/// sbokatuk/Net.Agora.iOS's native/AgoraFastboardBridge.
///
/// Fastboard coexists with every other Net.Agora product, and pulls in the whiteboard binding it
/// is built on.
/// </summary>
public interface IAgoraFastboardClient : IDisposable
{
    /// <summary>The room connection's lifecycle.</summary>
    event EventHandler<AgoraFastboardPhaseEventArgs>? PhaseChanged;

    /// <summary>
    /// The room ended without <see cref="Disconnect"/> being called — a failure, or the server
    /// removing this client.
    /// </summary>
    event EventHandler<AgoraFastboardDisconnectedEventArgs>? Disconnected;

    /// <summary>True between a successful join and <see cref="Disconnect"/> or a drop.</summary>
    bool IsJoined { get; }

    /// <summary>
    /// Joins the room named by the options, completing when the board is ready to draw on.
    /// </summary>
    /// <exception cref="AgoraFastboardException">
    /// The SDK reported an error, or did not answer within <see cref="AgoraFastboardOptions.Timeout"/>.
    /// </exception>
    Task JoinAsync(CancellationToken cancellationToken = default);

    /// <summary>Leaves the room. Safe to call when idle.</summary>
    void Disconnect();

    /// <summary>
    /// Picks the drawing tool, and optionally its colour — the same thing the toolbar does, for an
    /// app that wants its own controls alongside.
    /// </summary>
    /// <param name="tool">The tool to select.</param>
    /// <param name="color">The stroke colour as 0xRRGGBB, or null to leave it.</param>
    void SetTool(AgoraFastboardTool tool, uint? color = null);

    /// <summary>Undoes the last local operation.</summary>
    void Undo();

    /// <summary>Redoes the last undone local operation.</summary>
    void Redo();

    /// <summary>Erases the current page for everyone in the room.</summary>
    void Clear();

    /// <summary>
    /// Grants or revokes this client's ability to draw, completing when the room confirms.
    /// Fastboard hides its toolbar's drawing controls while read-only.
    /// </summary>
    /// <inheritdoc cref="JoinAsync" path="/exception" />
    Task SetWritableAsync(bool writable, CancellationToken cancellationToken = default);
}
