namespace Net.Agora.Fastboard;

/// <summary>Raised when a Fastboard operation fails, or the SDK reports an error.</summary>
public sealed class AgoraFastboardException(string message) : Exception(message);

/// <summary>The room connection's lifecycle. The same five states the whiteboard SDK reports.</summary>
public enum AgoraFastboardPhase
{
    /// <summary>A join is in flight.</summary>
    Connecting = 0,

    /// <summary>Joined.</summary>
    Connected = 1,

    /// <summary>The SDK lost the room and is re-establishing it on its own.</summary>
    Reconnecting = 2,

    /// <summary>A disconnect is in flight.</summary>
    Disconnecting = 3,

    /// <summary>Not in the room.</summary>
    Disconnected = 4,
}

/// <summary>Raised for <see cref="IAgoraFastboardClient.PhaseChanged"/>.</summary>
public sealed class AgoraFastboardPhaseEventArgs(AgoraFastboardPhase phase) : EventArgs
{
    /// <summary>The state the room connection moved to.</summary>
    public AgoraFastboardPhase Phase { get; } = phase;
}

/// <summary>Raised for <see cref="IAgoraFastboardClient.Disconnected"/>.</summary>
public sealed class AgoraFastboardDisconnectedEventArgs(string reason, bool kicked) : EventArgs
{
    /// <summary>What the SDK said — a message, not a code.</summary>
    public string Reason { get; } = reason;

    /// <summary>True when the server ended the session rather than the connection failing.</summary>
    public bool Kicked { get; } = kicked;
}

/// <summary>
/// A drawing tool. A subset of Fastboard's own appliance set — the shapes it adds beyond the
/// whiteboard SDK's (bubble, pentagram, rhombus, triangle) are reachable through the toolbar the
/// board draws for itself, which is the point of using Fastboard rather than the bare board.
/// </summary>
public enum AgoraFastboardTool
{
    /// <summary>Freehand drawing.</summary>
    Pencil,

    /// <summary>Erases whole strokes.</summary>
    Eraser,

    /// <summary>Selects and moves what is already drawn.</summary>
    Selector,

    /// <summary>Places a text box.</summary>
    Text,

    /// <summary>Draws a rectangle.</summary>
    Rectangle,

    /// <summary>Draws an ellipse.</summary>
    Ellipse,

    /// <summary>Draws a straight line.</summary>
    Straight,

    /// <summary>Draws an arrow.</summary>
    Arrow,

    /// <summary>Pans the camera instead of drawing.</summary>
    Hand,

    /// <summary>A pointer others can see, which leaves no marks.</summary>
    LaserPointer,

    /// <summary>Neither draws nor pans — for a read-only viewer.</summary>
    Clicker,
}
