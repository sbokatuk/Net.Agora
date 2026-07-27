namespace Net.Agora.Fastboard;

/// <summary>
/// The neutral-target-framework half of <see cref="AgoraFastboardClient"/> — the counterpart of
/// Platforms/Android and Platforms/Apple, compiled into the plain net8.0/net9.0/net10.0 build.
/// There is no board on these target frameworks; the constructor throws, and it is the only
/// reachable member.
/// </summary>
/// <remarks>
/// The leg exists so shared code — ViewModels, tests, heads for platforms Agora does not ship on
/// — can reference the package and program against <see cref="IAgoraFastboardClient"/> instead of
/// failing restore with NU1202. Create the real client in a net*-android / net*-ios application
/// head — in a MAUI app, from Net.Agora.Fastboard.Maui's <c>AgoraFastboardView.CreateClient</c> —
/// and inject it. The iOS half's <c>View</c> property does not exist here: its type is a platform
/// view, so code that shows the board is platform code by definition.
/// </remarks>
public sealed partial class AgoraFastboardClient
{
    /// <summary>Always throws — see the class remarks for where the real client is created.</summary>
    /// <exception cref="PlatformNotSupportedException">Always.</exception>
    public AgoraFastboardClient(AgoraFastboardOptions options)
    {
        // Assigned rather than discarded only so the shared half's field is assigned somewhere in
        // this compilation (CS0649); the throw makes the value unreachable.
        _options = options;
        throw NotSupported();
    }

    private static PlatformNotSupportedException NotSupported() => new(
        "Net.Agora.Fastboard has no board on this target framework — the neutral build exists so " +
        "shared code can reference the package and program against IAgoraFastboardClient. Create " +
        "the client in a net*-android or net*-ios application head — in a MAUI app, from " +
        "Net.Agora.Fastboard.Maui's AgoraFastboardView — and inject it.");

    // Everything below satisfies the shared half of the class. The constructor is the only
    // entry point and always throws, so none of it can run.

    private void JoinCore(Action<AgoraFastboardException?> complete) => throw NotSupported();

    private void DisconnectCore() => throw NotSupported();

    private void SetToolCore(AgoraFastboardTool tool, uint? color) => throw NotSupported();

    private void UndoCore() => throw NotSupported();

    private void RedoCore() => throw NotSupported();

    private void ClearCore() => throw NotSupported();

    private void SetWritableCore(bool writable, Action<AgoraFastboardException?> complete) =>
        throw NotSupported();

    private void DisposeCore()
    {
        // Nothing to release: construction always throws, so no instance ever holds resources.
    }
}
