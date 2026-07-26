using Net.Agora.Chat;
using Net.Agora.Fastboard;
using Net.Agora.Signaling;
using Net.Agora.Video;
using Net.Agora.Voice;
using Net.Agora.Whiteboard;

namespace Net.Agora;

/// <summary>
/// Pins down the one behaviour the neutral (plain net8.0/net9.0/net10.0) leg of each façade has:
/// constructing a client throws <see cref="PlatformNotSupportedException"/>, with a message that
/// says where the real client comes from. This test project resolves each façade's real neutral
/// assembly (see the csproj), so these six constructors are exactly what a consumer's shared
/// class library would hit.
/// </summary>
/// <remarks>
/// The message assertions name the platform heads rather than pin exact wording, so the messages
/// can be reworded freely — but one that stops telling the reader where to construct the real
/// client fails. The options are deliberately left unconfigured: the neutral constructors throw
/// before validating anything, so not even a missing AppId gets in first.
/// </remarks>
public class NeutralPlatformTests
{
    [Fact]
    public void Video_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraVideoClient(new AgoraVideoOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Voice_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraVoiceClient(new AgoraVoiceOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Signaling_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraSignalingClient(new AgoraSignalingOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Chat_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraChatClient(new AgoraChatOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Whiteboard_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraWhiteboardClient(new AgoraWhiteboardOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }

    [Fact]
    public void Fastboard_neutral_constructor_throws_and_names_the_platform_heads()
    {
        var error = Assert.Throws<PlatformNotSupportedException>(
            () => new AgoraFastboardClient(new AgoraFastboardOptions()));

        Assert.Contains("net*-android", error.Message);
        Assert.Contains("net*-ios", error.Message);
    }
}
