namespace Net.Agora.Whiteboard;

/// <summary>
/// The neutral-target-framework half of <see cref="AgoraWhiteboardClient"/> — the counterpart of
/// Platforms/Android and Platforms/Apple, compiled into the plain net8.0/net9.0/net10.0 build.
/// There is no board on these target frameworks; the constructor throws, and it is the only
/// reachable member.
/// </summary>
/// <remarks>
/// The leg exists so shared code — ViewModels, tests, heads for platforms Agora does not ship on
/// — can reference the package and program against <see cref="IAgoraWhiteboardClient"/> instead
/// of failing restore with NU1202. Both platform constructors take the SDK's board view — a
/// platform type, so this constructor cannot: it takes only the options, and throws. Create the
/// real client in a net*-android / net*-ios application head — in a MAUI app, from
/// Net.Agora.Whiteboard.Maui's <c>AgoraWhiteboardView.CreateClient</c> — and inject it.
/// </remarks>
public sealed partial class AgoraWhiteboardClient
{
    /// <summary>Always throws — see the class remarks for where the real client is created.</summary>
    /// <exception cref="PlatformNotSupportedException">Always.</exception>
    public AgoraWhiteboardClient(AgoraWhiteboardOptions options)
    {
        // Assigned rather than discarded only so the shared half's field is assigned somewhere in
        // this compilation (CS0649); the throw makes the value unreachable.
        _options = options;
        throw NotSupported();
    }

    private static PlatformNotSupportedException NotSupported() => new(
        "Net.Agora.Whiteboard has no board on this target framework — the neutral build exists " +
        "so shared code can reference the package and program against IAgoraWhiteboardClient. " +
        "Create the client in a net*-android or net*-ios application head — in a MAUI app, from " +
        "Net.Agora.Whiteboard.Maui's AgoraWhiteboardView — and inject it.");

    // Everything below satisfies the shared half of the class. The constructor is the only
    // entry point and always throws, so none of it can run.

    private void JoinCore(Action<AgoraWhiteboardException?> complete) => throw NotSupported();

    private void DisconnectCore() => throw NotSupported();

    private void SetToolCore(string applianceName, int[]? rgb, double? strokeWidth) => throw NotSupported();

    private void UndoCore() => throw NotSupported();

    private void RedoCore() => throw NotSupported();

    private void ClearCore(bool retainDocument) => throw NotSupported();

    private void SetWritableCore(bool writable, Action<AgoraWhiteboardException?> complete) =>
        throw NotSupported();

    private void RefreshViewSizeCore() => throw NotSupported();

    private void DisposeCore()
    {
        // Nothing to release: construction always throws, so no instance ever holds resources.
    }
}
