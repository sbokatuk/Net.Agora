namespace Net.Agora.Fastboard;

/// <summary>
/// What <see cref="AgoraFastboardClient"/> needs to join a room. The same four identifiers
/// <c>Net.Agora.Whiteboard</c> takes, because Fastboard is a UI layer over the same service — the
/// App Identifier from the Agora Console, the room UUID and token from your own server's call to
/// the whiteboard REST API.
/// </summary>
public sealed class AgoraFastboardOptions
{
    /// <summary>The whiteboard App Identifier from the Agora Console, shaped "xxx/yyy". Required.</summary>
    public string? AppIdentifier { get; set; }

    /// <summary>The room's UUID. Required.</summary>
    public string? RoomUuid { get; set; }

    /// <summary>The room token for <see cref="RoomUuid"/>. Required.</summary>
    public string? RoomToken { get; set; }

    /// <summary>The user ID to join as. Required, and unique per connection.</summary>
    public string? Uid { get; set; }

    /// <summary>
    /// The data-centre region, as one of netless's own keys — "cn-hz", "us-sv", "sg", "in-mum",
    /// "gb-lon". Null takes the SDK's default, which is "cn-hz".
    /// </summary>
    public string? Region { get; set; }

    /// <summary>Whether this client may draw. False joins read-only; the board still renders.</summary>
    public bool Writable { get; set; } = true;

    /// <summary>How long <see cref="IAgoraFastboardClient.JoinAsync"/> waits before failing.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Throws <see cref="ArgumentException"/> if a required field is missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppIdentifier))
        {
            throw new ArgumentException("AgoraFastboardOptions.AppIdentifier is required.", nameof(AppIdentifier));
        }

        if (string.IsNullOrWhiteSpace(RoomUuid))
        {
            throw new ArgumentException("AgoraFastboardOptions.RoomUuid is required.", nameof(RoomUuid));
        }

        if (string.IsNullOrWhiteSpace(RoomToken))
        {
            throw new ArgumentException("AgoraFastboardOptions.RoomToken is required.", nameof(RoomToken));
        }

        if (string.IsNullOrWhiteSpace(Uid))
        {
            throw new ArgumentException("AgoraFastboardOptions.Uid is required.", nameof(Uid));
        }
    }
}
