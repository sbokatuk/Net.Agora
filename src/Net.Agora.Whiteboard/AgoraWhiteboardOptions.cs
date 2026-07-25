namespace Net.Agora.Whiteboard;

/// <summary>
/// What <see cref="AgoraWhiteboardClient"/> needs to join a room. Construct one, set the required
/// fields, and pass it to the constructor — see <see cref="Validate"/> for what is required.
/// </summary>
public sealed class AgoraWhiteboardOptions
{
    /// <summary>
    /// The whiteboard App Identifier from the Agora Console. Required. Note this is not an RTC
    /// App ID: the Interactive Whiteboard is a separate service with its own credentials, and the
    /// identifier has the shape "xxx/yyy".
    /// </summary>
    public string? AppIdentifier { get; set; }

    /// <summary>The room's UUID, from your own server's call to the whiteboard REST API. Required.</summary>
    public string? RoomUuid { get; set; }

    /// <summary>The room token for <see cref="RoomUuid"/>, from the same place. Required.</summary>
    public string? RoomToken { get; set; }

    /// <summary>The user ID to join as. Required, and unique per connection.</summary>
    public string? Uid { get; set; }

    /// <summary>
    /// The data-centre region, as one of netless's own keys — "cn-hz", "us-sv", "sg", "in-mum",
    /// "gb-lon". Null takes the SDK's default. A string rather than an enum because both SDKs
    /// declare it as one, and the set grows without an SDK release.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Whether this client may draw. False joins read-only — the board still renders and follows.
    /// Changeable afterwards with <see cref="IAgoraWhiteboardClient.SetWritableAsync"/>.
    /// </summary>
    public bool Writable { get; set; } = true;

    /// <summary>Whether the SDK writes its own log to the platform log. Off by default.</summary>
    public bool EnableLog { get; set; }

    /// <summary>How long <see cref="IAgoraWhiteboardClient.JoinAsync"/> waits before failing.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Throws <see cref="ArgumentException"/> if a required field is missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppIdentifier))
        {
            throw new ArgumentException("AgoraWhiteboardOptions.AppIdentifier is required.", nameof(AppIdentifier));
        }

        if (string.IsNullOrWhiteSpace(RoomUuid))
        {
            throw new ArgumentException("AgoraWhiteboardOptions.RoomUuid is required.", nameof(RoomUuid));
        }

        if (string.IsNullOrWhiteSpace(RoomToken))
        {
            throw new ArgumentException("AgoraWhiteboardOptions.RoomToken is required.", nameof(RoomToken));
        }

        if (string.IsNullOrWhiteSpace(Uid))
        {
            throw new ArgumentException("AgoraWhiteboardOptions.Uid is required.", nameof(Uid));
        }
    }
}
