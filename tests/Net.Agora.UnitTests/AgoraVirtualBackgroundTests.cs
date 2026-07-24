namespace Net.Agora.Video;

/// <summary>
/// Covers <see cref="AgoraVirtualBackground"/>'s factory methods — the one piece of extension
/// surface with logic of its own rather than a straight pass-through to the engine. The three
/// source-type values are the SDKs' own and are shared by both platforms, so getting one wrong
/// silently selects the wrong kind of background.
/// </summary>
public class AgoraVirtualBackgroundTests
{
    [Fact]
    public void Blurred_selects_the_blur_source_and_carries_the_degree()
    {
        var background = AgoraVirtualBackground.Blurred(AgoraBackgroundBlur.High);

        Assert.Equal(3, background.SourceType);
        Assert.Equal(AgoraBackgroundBlur.High, background.Blur);
        Assert.Null(background.Path);
    }

    [Fact]
    public void Blurred_defaults_to_a_medium_degree()
    {
        Assert.Equal(AgoraBackgroundBlur.Medium, AgoraVirtualBackground.Blurred().Blur);
    }

    [Fact]
    public void SolidColor_selects_the_colour_source_and_carries_the_rgb()
    {
        var background = AgoraVirtualBackground.SolidColor(0x00FF7F);

        Assert.Equal(1, background.SourceType);
        Assert.Equal(0x00FF7Fu, background.Color);
    }

    [Fact]
    public void Image_selects_the_image_source_and_carries_the_path()
    {
        var background = AgoraVirtualBackground.Image("/tmp/office.png");

        Assert.Equal(2, background.SourceType);
        Assert.Equal("/tmp/office.png", background.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Image_rejects_an_empty_path(string path)
    {
        Assert.Throws<ArgumentException>(() => AgoraVirtualBackground.Image(path));
    }
}
