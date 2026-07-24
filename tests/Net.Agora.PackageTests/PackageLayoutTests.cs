namespace Net.Agora.PackageTests;

/// <summary>
/// Asserts the shape of this repository's own packed NuGet packages: the cross-platform façades
/// (Net.Agora.Video, Net.Agora.Voice) and their MAUI companions. Runs against the packed .nupkg
/// rather than the build output, so it catches packaging regressions the compiler cannot see.
///
/// The platform packages are not built in this repository — they are published from
/// sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS, which carry their own package-shape
/// tests (native .aar/xcframework payload, their own target frameworks).
/// </summary>
public class PackageLayoutTests
{
    [Theory]
    [MemberData(nameof(Packages.FacadeAndroidFrameworks), MemberType = typeof(Packages))]
    public void Facade_carries_an_assembly_for_every_android_target_framework(string facade, string tfm)
    {
        using var package = Packages.OpenPackage(facade);

        var expected = $"lib/{tfm}/{facade}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{facade} is missing '{expected}'.");
    }

    [SkippableTheory]
    [MemberData(nameof(Packages.FacadeIosFrameworks), MemberType = typeof(Packages))]
    public void Facade_carries_an_assembly_for_every_ios_target_framework(string facade, string tfm)
    {
        Skip.IfNot(Packages.Exists(facade), $"{facade} was not packed");

        using var package = Packages.OpenPackage(facade);

        var expected = $"lib/{tfm}/{facade}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{facade} is missing '{expected}'.");
    }

    [Theory]
    [MemberData(nameof(Packages.ProductRows), MemberType = typeof(Packages))]
    public void Metapackage_depends_on_the_platform_bindings_at_the_pinned_versions(
        string facade, string maui, string android, string ios)
    {
        _ = maui;

        using var package = Packages.OpenPackage(facade);
        var nuspec = Packages.ReadNuspec(package, facade);

        var dependencies = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .ToList();

        var ids = dependencies.Select(d => d.Attribute("id")?.Value).ToHashSet();

        Assert.Contains(android, ids);
        Assert.Contains(ios, ids);

        // Exact pin, not a floating range: both are separate repositories on their own release
        // cadence, and a floating reference would turn an unrelated upstream change into a build
        // break here rather than there — see Directory.Build.props.
        foreach (var dependency in dependencies.Where(d =>
                     d.Attribute("id")?.Value == android || d.Attribute("id")?.Value == ios))
        {
            var version = dependency.Attribute("version")?.Value;
            Assert.True(
                version is not null && version.StartsWith('[') && version.EndsWith(']'),
                $"{dependency.Attribute("id")?.Value} dependency version '{version}' is not an " +
                "exact pin ([x.y.z]) — a floating range would silently resolve a different " +
                "platform package than the one this build was verified against.");
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Packages.ProductRows), MemberType = typeof(Packages))]
    public void Maui_package_depends_on_the_metapackage(
        string facade, string maui, string android, string ios)
    {
        _ = (android, ios);

        Skip.IfNot(Packages.Exists(maui), $"{maui} was not packed");

        using var package = Packages.OpenPackage(maui);
        var nuspec = Packages.ReadNuspec(package, maui);

        var ids = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => e.Attribute("id")?.Value)
            .ToHashSet();

        Assert.Contains(facade, ids);
    }
}
