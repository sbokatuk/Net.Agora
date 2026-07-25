namespace Net.Agora.Whiteboard;

/// <summary>
/// Joins an Agora Interactive Whiteboard room and draws on it, with the same surface on Android
/// and iOS.
///
/// The platform bindings underneath do not resemble each other — Android's <c>WhiteSdk</c> reports
/// a join through a Java <c>Promise</c>, iOS's <c>WhiteSDK</c> through an Objective-C completion
/// block — and this is the layer that hides the difference. Reach for <c>Agora.Whiteboard.*</c>
/// (Android, via Net.Agora.Whiteboard.Android) or <c>Net.Agora.Whiteboard.iOS.*</c> directly for
/// anything not exposed here: replay, multi-window document apps, PPT conversion, the synced
/// key-value store, custom events.
///
/// The board is a web view on both platforms rather than a native canvas, which is why operations
/// that look local — changing the tool, undoing — are messages across a JavaScript bridge. It also
/// means the board needs a view: setting the render target is platform-specific and therefore not
/// part of this interface. Construct the client with the platform's board view, or let
/// Net.Agora.Whiteboard.Maui's <c>AgoraWhiteboardView</c> do it.
///
/// The Interactive Whiteboard coexists with every other Net.Agora product — it shares no native
/// library with any of them.
/// </summary>
public interface IAgoraWhiteboardClient : IDisposable
{
    /// <summary>The room connection's lifecycle.</summary>
    event EventHandler<AgoraWhiteboardPhaseEventArgs>? PhaseChanged;

    /// <summary>
    /// The room ended without <see cref="Disconnect"/> being called — a connection failure, or the
    /// server removing this client.
    /// </summary>
    event EventHandler<AgoraWhiteboardDisconnectedEventArgs>? Disconnected;

    /// <summary>
    /// How many undo and redo steps are available, as the board reports them. Drives an
    /// undo/redo button's enabled state without polling.
    /// </summary>
    event EventHandler<AgoraWhiteboardHistoryEventArgs>? HistoryChanged;

    /// <summary>True between a successful join and <see cref="Disconnect"/> or a drop.</summary>
    bool IsJoined { get; }

    /// <summary>
    /// Joins the room named by the options, completing when the board is ready to draw on.
    /// </summary>
    /// <exception cref="AgoraWhiteboardException">
    /// The SDK reported an error, or did not answer within <see cref="AgoraWhiteboardOptions.Timeout"/>.
    /// </exception>
    Task JoinAsync(CancellationToken cancellationToken = default);

    /// <summary>Leaves the room. Safe to call when idle.</summary>
    void Disconnect();

    /// <summary>
    /// Picks the drawing tool, and optionally its colour and stroke width. Colour is 0xRRGGBB;
    /// null leaves the current one.
    /// </summary>
    /// <param name="tool">The tool to select.</param>
    /// <param name="color">The stroke colour as 0xRRGGBB, or null to leave it.</param>
    /// <param name="strokeWidth">The stroke width in points, or null to leave it.</param>
    void SetTool(AgoraWhiteboardTool tool, uint? color = null, double? strokeWidth = null);

    /// <summary>Undoes the last local operation.</summary>
    void Undo();

    /// <summary>Redoes the last undone local operation.</summary>
    void Redo();

    /// <summary>
    /// Erases the current page for everyone in the room.
    /// </summary>
    /// <param name="retainDocument">Keeps a converted document's page as the background.</param>
    void Clear(bool retainDocument = true);

    /// <summary>
    /// Grants or revokes this client's ability to draw, completing when the room confirms.
    /// </summary>
    /// <inheritdoc cref="JoinAsync" path="/exception" />
    Task SetWritableAsync(bool writable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the board its view changed size. Neither SDK watches its own view's bounds, so a
    /// rotation or a layout change leaves the drawing surface at the old size until this is called.
    /// </summary>
    void RefreshViewSize();
}

/// <summary>Raised for <see cref="IAgoraWhiteboardClient.HistoryChanged"/>.</summary>
public sealed class AgoraWhiteboardHistoryEventArgs(int undoSteps, int redoSteps) : EventArgs
{
    /// <summary>How many operations can be undone.</summary>
    public int UndoSteps { get; } = undoSteps;

    /// <summary>How many can be redone.</summary>
    public int RedoSteps { get; } = redoSteps;
}
