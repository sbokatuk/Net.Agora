namespace Net.Agora.PackageTests;

/// <summary>
/// Asserts the shape of this repository's own packed NuGet packages: Net.Agora.Video (the
/// cross-platform façade) and Net.Agora.Video.Maui. Runs against the packed .nupkg rather than
/// the build output, so it catches packaging regressions the compiler cannot see.
///
/// Net.Agora.Video.Android and Net.Agora.Video.iOS are not built in this repository anymore —
/// they are published from sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS, which carry
/// their own package-shape tests (native .aar/xcframework payload, their own target frameworks).
/// </summary>
public class PackageLayoutTests
{
    [Theory]
    [MemberData(nameof(Packages.AndroidFrameworks), MemberType = typeof(Packages))]
    public void Video_carries_a_binding_assembly_for_every_android_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Video);

        var expected = $"lib/{tfm}/{Packages.Video}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{Packages.Video} is missing '{expected}'.");
    }

    [SkippableTheory]
    [MemberData(nameof(Packages.IosFrameworks), MemberType = typeof(Packages))]
    public void Video_carries_a_binding_assembly_for_every_ios_target_framework(string tfm)
    {
        Skip.IfNot(Packages.Exists(Packages.Video), $"{Packages.Video} was not packed");

        using var package = Packages.OpenPackage(Packages.Video);

        var expected = $"lib/{tfm}/{Packages.Video}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{Packages.Video} is missing '{expected}'.");
    }

    [Fact]
    public void Metapackage_depends_on_the_platform_bindings_at_the_pinned_versions()
    {
        using var package = Packages.OpenPackage(Packages.Video);
        var nuspec = Packages.ReadNuspec(package, Packages.Video);

        var dependencies = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .ToList();

        var ids = dependencies.Select(d => d.Attribute("id")?.Value).ToHashSet();

        Assert.Contains(Packages.VideoAndroid, ids);
        Assert.Contains(Packages.VideoIOS, ids);

        // Exact pin, not a floating range: both are separate repositories on their own release
        // cadence, and a floating reference would turn an unrelated upstream change into a build
        // break here rather than there — see Directory.Build.props.
        foreach (var dependency in dependencies.Where(d =>
                     d.Attribute("id")?.Value is Packages.VideoAndroid or Packages.VideoIOS))
        {
            var version = dependency.Attribute("version")?.Value;
            Assert.True(
                version is not null && version.StartsWith('[') && version.EndsWith(']'),
                $"{dependency.Attribute("id")?.Value} dependency version '{version}' is not an " +
                "exact pin ([x.y.z]) — a floating range would silently resolve a different " +
                "platform package than the one this build was verified against.");
        }
    }

    [SkippableFact]
    public void Maui_package_depends_on_the_metapackage()
    {
        Skip.IfNot(Packages.Exists(Packages.VideoMaui), $"{Packages.VideoMaui} was not packed");

        using var package = Packages.OpenPackage(Packages.VideoMaui);
        var nuspec = Packages.ReadNuspec(package, Packages.VideoMaui);

        var ids = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => e.Attribute("id")?.Value)
            .ToHashSet();

        Assert.Contains(Packages.Video, ids);
    }
}
