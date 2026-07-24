namespace Net.Agora.Voice.Maui;

/// <summary>
/// Creates clients without the caller writing platform code.
///
/// This is the whole reason the MAUI package exists: on Android the engine needs a <c>Context</c>,
/// so a MAUI app would otherwise need an <c>#if ANDROID</c> block for construction. Voice renders
/// nothing, so unlike Net.Agora.Video.Maui there is no view type and no handler here.
/// </summary>
public static class AgoraVoiceClientExtensions
{
    /// <summary>Creates a client for the current platform.</summary>
    /// <exception cref="InvalidOperationException">
    /// Android only, and only when called before the activity exists — from a page constructor,
    /// for example. Create the client after the page has appeared.
    /// </exception>
    public static AgoraVoiceClient CreateClient(this AgoraVoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

#if ANDROID
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException(
                "no current activity. RtcEngineConfig needs a Context, so create the client " +
                "after the page has appeared rather than in its constructor.");

        return new AgoraVoiceClient(options, activity);
#else
        return new AgoraVoiceClient(options);
#endif
    }
}
