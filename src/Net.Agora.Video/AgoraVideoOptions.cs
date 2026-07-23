namespace Net.Agora.Video;

/// <summary>The channel profile — see Agora's <c>AgoraChannelProfile</c> / <c>Constants.CHANNEL_PROFILE_*</c>.</summary>
public enum AgoraChannelProfile
{
    /// <summary>Two peers, both publishing and subscribing.</summary>
    Communication = 0,

    /// <summary>One or more broadcasters and many audience members; pair with <see cref="AgoraClientRole"/>.</summary>
    LiveBroadcasting = 1,
}

/// <summary>The client's role in a <see cref="AgoraChannelProfile.LiveBroadcasting"/> channel.</summary>
public enum AgoraClientRole
{
    /// <summary>Publishes and is visible to the audience.</summary>
    Broadcaster = 1,

    /// <summary>Subscribes only.</summary>
    Audience = 2,
}

/// <summary>
/// What <see cref="AgoraVideoClient"/> needs to join a channel. Construct one, set the required
/// fields, and pass it to the constructor — see <see cref="Validate"/> for what is required.
/// </summary>
public sealed class AgoraVideoOptions
{
    /// <summary>The App ID from the Agora Console. Required.</summary>
    public string? AppId { get; set; }

    /// <summary>The channel to join. Required — set on the instance passed to <c>JoinAsync</c>, not here.</summary>
    public string? ChannelId { get; set; }

    /// <summary>
    /// The token for <see cref="ChannelId"/>, or null for an app with App ID-only authentication
    /// (testing only — Agora requires tokens in production).
    /// </summary>
    public string? Token { get; set; }

    /// <summary>The uid to join as, or 0 to let Agora assign one.</summary>
    public uint Uid { get; set; }

    /// <inheritdoc cref="AgoraChannelProfile" />
    public AgoraChannelProfile ChannelProfile { get; set; } = AgoraChannelProfile.Communication;

    /// <summary>Only meaningful when <see cref="ChannelProfile"/> is <see cref="AgoraChannelProfile.LiveBroadcasting"/>.</summary>
    public AgoraClientRole ClientRole { get; set; } = AgoraClientRole.Broadcaster;

    /// <summary>How long <c>JoinAsync</c> waits for the server to confirm before failing.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Throws <see cref="ArgumentException"/> if a required field is missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppId))
        {
            throw new ArgumentException("AgoraVideoOptions.AppId is required.", nameof(AppId));
        }
    }
}
