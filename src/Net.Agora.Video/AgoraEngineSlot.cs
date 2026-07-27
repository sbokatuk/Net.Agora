namespace Net.Agora.Video;

/// <summary>
/// Tracks the one live client this process may have, because the Agora RTC engine underneath is
/// a process-wide singleton on both platforms — iOS's <c>+[AgoraRtcEngineKit sharedEngineWithConfig:delegate:]</c>
/// and Android's <c>RtcEngine.create</c> hand back the same engine to every caller, and both are
/// torn down by a <em>static</em> destroy.
/// </summary>
/// <remarks>
/// Without this, a second client silently re-points the native callbacks at itself — the first
/// client stops receiving events with nothing logged — and disposing either one destroys the
/// engine for both. Neither failure names its cause, so the second construction is refused
/// instead, at the point the mistake is made.
/// </remarks>
internal static class AgoraEngineSlot
{
    private static int _taken;

    /// <summary>
    /// Reserves the process's engine for a client about to be constructed. Call it after the
    /// options validate and immediately before creating the native engine, and
    /// <see cref="Release"/> it if construction then fails.
    /// </summary>
    /// <exception cref="InvalidOperationException">A client is already live in this process.</exception>
    internal static void Acquire()
    {
        if (Interlocked.CompareExchange(ref _taken, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "An AgoraVideoClient is already live in this process. The Agora RTC engine is a " +
                "process-wide singleton: a second client would take over the first one's " +
                "callbacks, and disposing either would destroy the engine for both. Reuse the " +
                "existing client, or dispose it before creating another.");
        }
    }

    /// <summary>Returns the engine to the pool — on dispose, or when construction failed.</summary>
    internal static void Release() => Interlocked.Exchange(ref _taken, 0);
}
