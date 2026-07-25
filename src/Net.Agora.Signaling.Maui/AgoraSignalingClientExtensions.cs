namespace Net.Agora.Signaling.Maui;

/// <summary>
/// A <see cref="CreateClient"/> that matches the other products' MAUI packages, so every Agora
/// client is constructed the same way in a MAUI app.
///
/// Unlike Net.Agora.Video.Maui / Voice.Maui / Chat.Maui, this does no platform work: Signaling
/// takes no Android <c>Context</c> and renders nothing, so there is nothing to supply. The method
/// is a straight call to the constructor — its only value is that
/// <c>options.CreateClient()</c> reads identically across products. Reference
/// <c>Net.Agora.Signaling</c> and use <c>new AgoraSignalingClient(options)</c> if you do not want
/// the MAUI dependency.
/// </summary>
public static class AgoraSignalingClientExtensions
{
    /// <summary>Creates a client from the options — the same as <c>new AgoraSignalingClient(options)</c>.</summary>
    public static AgoraSignalingClient CreateClient(this AgoraSignalingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // No #if ANDROID here, unlike the RTC and Chat companions: RTM's constructor is the same
        // on both platforms because it needs no Context.
        return new AgoraSignalingClient(options);
    }
}
