namespace Net.Agora.Chat;

/// <summary>
/// The neutral-target-framework half of <see cref="AgoraChatClient"/> — the counterpart of
/// Platforms/Android and Platforms/Apple, compiled into the plain net8.0/net9.0/net10.0 build.
/// There is no native client on these target frameworks; the constructor throws, and it is the
/// only reachable member.
/// </summary>
/// <remarks>
/// The leg exists so shared code — ViewModels, tests, heads for platforms Agora does not ship on
/// — can reference the package and program against <see cref="IAgoraChatClient"/> instead of
/// failing restore with NU1202. Construct the real client in a net*-android / net*-ios
/// application (directly, or through Net.Agora.Chat.Maui's <c>CreateClient</c>) and inject it.
/// </remarks>
public sealed partial class AgoraChatClient
{
    /// <summary>Always throws — see the class remarks for where to construct the real client.</summary>
    /// <exception cref="PlatformNotSupportedException">Always.</exception>
    public AgoraChatClient(AgoraChatOptions options)
    {
        // Assigned rather than discarded only so the shared half's field is assigned somewhere in
        // this compilation (CS0649); the throw makes the value unreachable.
        _options = options;
        throw NotSupported();
    }

    private static PlatformNotSupportedException NotSupported() => new(
        "Net.Agora.Chat has no native client on this target framework — the neutral build exists " +
        "so shared code can reference the package and program against IAgoraChatClient. " +
        "Construct the client in a net*-android or net*-ios application head and inject it.");

    // Everything below satisfies the shared half of the class. The constructor is the only
    // entry point and always throws, so none of it can run.

    /// <inheritdoc cref="IAgoraChatClient.CurrentUserId" />
    public string? CurrentUserId => throw NotSupported();

    private void LoginCore(string userId, string token, Action<AgoraChatException?> complete) =>
        throw NotSupported();

    private void LogoutCore(bool unbindDeviceToken, Action<AgoraChatException?> complete) =>
        throw NotSupported();

    private void RenewTokenCore(string token, Action<AgoraChatException?> complete) =>
        throw NotSupported();

    private void SendTextMessageCore(
        string conversationId,
        string text,
        AgoraChatType chatType,
        Action<AgoraChatMessage, AgoraChatException?> complete) => throw NotSupported();

    private IReadOnlyList<AgoraChatConversation> GetConversationsCore() => throw NotSupported();

    private void DisposeCore()
    {
        // Nothing to release: construction always throws, so no instance ever holds resources.
    }
}
