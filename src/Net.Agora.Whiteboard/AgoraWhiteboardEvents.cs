namespace Net.Agora.Whiteboard;

/// <summary>Raised when a whiteboard operation fails, or the SDK reports an error.</summary>
public sealed class AgoraWhiteboardException(string message) : Exception(message);

/// <summary>The room connection's lifecycle. Both SDKs report the same five states.</summary>
public enum AgoraWhiteboardPhase
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

/// <summary>Raised for <see cref="IAgoraWhiteboardClient.PhaseChanged"/>.</summary>
public sealed class AgoraWhiteboardPhaseEventArgs(AgoraWhiteboardPhase phase) : EventArgs
{
    /// <summary>The state the room connection moved to.</summary>
    public AgoraWhiteboardPhase Phase { get; } = phase;
}

/// <summary>Raised for <see cref="IAgoraWhiteboardClient.Disconnected"/>.</summary>
public sealed class AgoraWhiteboardDisconnectedEventArgs(string reason, bool kicked) : EventArgs
{
    /// <summary>What the SDK said — a server message or an exception's text, not a code.</summary>
    public string Reason { get; } = reason;

    /// <summary>True when the server ended the session rather than the connection failing.</summary>
    public bool Kicked { get; } = kicked;
}

/// <summary>
/// A drawing tool. The values are the SDK's own appliance-name strings, which both platforms and
/// the board's JavaScript share — so the enum is a spelling aid rather than a translation.
/// </summary>
public enum AgoraWhiteboardTool
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
