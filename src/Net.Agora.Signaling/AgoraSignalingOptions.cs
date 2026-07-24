namespace Net.Agora.Signaling;

/// <summary>
/// What <see cref="AgoraSignalingClient"/> needs to log in. Construct one, set the required
/// fields, and pass it to the constructor — see <see cref="Validate"/> for what is required.
/// </summary>
public sealed class AgoraSignalingOptions
{
    /// <summary>The App ID from the Agora Console. Required.</summary>
    public string? AppId { get; set; }

    /// <summary>
    /// The user ID to log in as. Required, and unique per connection: logging in twice with the
    /// same user ID kicks the earlier session off.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The RTM token for <see cref="UserId"/>, or null for an app with App ID-only authentication
    /// (testing only — Agora requires tokens in production).
    /// </summary>
    public string? Token { get; set; }

    /// <summary>How long the asynchronous operations wait for the service before failing.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Throws <see cref="ArgumentException"/> if a required field is missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppId))
        {
            throw new ArgumentException("AgoraSignalingOptions.AppId is required.", nameof(AppId));
        }

        if (string.IsNullOrWhiteSpace(UserId))
        {
            throw new ArgumentException("AgoraSignalingOptions.UserId is required.", nameof(UserId));
        }
    }
}
