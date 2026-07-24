namespace Net.Agora.Chat;

/// <summary>
/// What <see cref="AgoraChatClient"/> needs to sign in. Construct one, set the required fields,
/// and pass it to the constructor — see <see cref="Validate"/> for what is required.
/// </summary>
public sealed class AgoraChatOptions
{
    /// <summary>
    /// The App ID from the Agora Console. Required unless <see cref="AppKey"/> is set instead.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// The Easemob-style app key, for a Chat project provisioned before Agora unified the two.
    /// An alternative to <see cref="AppId"/>: set exactly one of them. Both still work, and which
    /// one an app uses is decided by how its project was created, not by preference.
    /// </summary>
    public string? AppKey { get; set; }

    /// <summary>
    /// The user ID to sign in as. Required, and unique per connection: signing in twice with the
    /// same user ID kicks the earlier session off.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The Chat token for <see cref="UserId"/>. Required — unlike Signaling, Chat has no
    /// App ID-only mode, so there is no testing shortcut around a token server.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Whether the SDK signs back in by itself on the next launch using its stored credentials.
    /// Off by default here, so a client's state is decided by the calls this API makes rather
    /// than by what a previous run left on disk.
    /// </summary>
    public bool AutoLogin { get; set; }

    /// <summary>Whether the SDK writes its own log to the console. Off by default.</summary>
    public bool EnableConsoleLog { get; set; }

    /// <summary>How long the asynchronous operations wait for the service before failing.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Throws <see cref="ArgumentException"/> if a required field is missing.</summary>
    public void Validate()
    {
        var hasAppId = !string.IsNullOrWhiteSpace(AppId);
        var hasAppKey = !string.IsNullOrWhiteSpace(AppKey);

        if (hasAppId == hasAppKey)
        {
            throw new ArgumentException(
                hasAppId
                    ? "set exactly one of AgoraChatOptions.AppId and AgoraChatOptions.AppKey, not both."
                    : "AgoraChatOptions.AppId (or AgoraChatOptions.AppKey) is required.",
                nameof(AppId));
        }

        if (string.IsNullOrWhiteSpace(UserId))
        {
            throw new ArgumentException("AgoraChatOptions.UserId is required.", nameof(UserId));
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new ArgumentException(
                "AgoraChatOptions.Token is required — Chat has no App ID-only authentication mode.",
                nameof(Token));
        }
    }
}
