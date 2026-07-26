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

    [SkippableTheory]
    [MemberData(nameof(Packages.FacadeMacosFrameworks), MemberType = typeof(Packages))]
    public void Facade_carries_an_assembly_for_every_macos_target_framework(string facade, string tfm)
    {
        // The macOS leg is only built on macOS, so on a Linux run this skips rather than fails —
        // same posture as the iOS leg. Only Video/Voice/Signaling have a macOS leg at all.
        Skip.IfNot(Packages.Exists(facade), $"{facade} was not packed");

        using var package = Packages.OpenPackage(facade);

        var expected = $"lib/{tfm}/{facade}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{facade} is missing '{expected}'.");
    }

    [Theory]
    [MemberData(nameof(Packages.FacadeNeutralFrameworks), MemberType = typeof(Packages))]
    public void Facade_carries_an_assembly_and_docs_for_every_neutral_target_framework(
        string facade, string tfm)
    {
        using var package = Packages.OpenPackage(facade);

        // The XML is asserted alongside the assembly for this leg only: the neutral build is the
        // one shared code programs against in an IDE, so losing its IntelliSense docs is a
        // packaging regression in its own right.
        foreach (var extension in new[] { "dll", "xml" })
        {
            var expected = $"lib/{tfm}/{facade}.{extension}";
            Assert.True(package.GetEntry(expected) is not null, $"{facade} is missing '{expected}'.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.FacadeRows), MemberType = typeof(Packages))]
    public void Facade_neutral_dependency_groups_pin_no_platform_packages(string facade)
    {
        using var package = Packages.OpenPackage(facade);
        var nuspec = Packages.ReadNuspec(package, facade);

        foreach (var tfm in Packages.NeutralTargetFrameworks)
        {
            var group = nuspec.Descendants()
                .SingleOrDefault(e => e.Name.LocalName == "group"
                    && e.Attribute("targetFramework")?.Value == tfm);

            // The group must exist — it is how NuGet advertises the leg to a plain-net restore
            // (net10.0's arrives via the two-pass merge) — and it must pin no Net.Agora.* platform
            // package: a platform pin leaking in here would make every plain-net restore chase a
            // binding it cannot use, failing with the very NU1202 the neutral leg exists to avoid.
            Assert.True(group is not null, $"{facade} has no '{tfm}' dependency group.");

            var leaked = group!.Descendants()
                .Where(e => e.Name.LocalName == "dependency")
                .Select(e => e.Attribute("id")?.Value)
                .Where(id => id is not null && id.StartsWith("Net.Agora.", StringComparison.Ordinal))
                .ToList();
            Assert.True(
                leaked.Count == 0,
                $"{facade}'s '{tfm}' dependency group pins {string.Join(", ", leaked)} — platform " +
                "packages must stay in the platform groups.");
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Packages.MacProductRows), MemberType = typeof(Packages))]
    public void Metapackage_depends_on_the_macos_binding_at_the_pinned_version(string facade, string mac)
    {
        Skip.IfNot(Packages.Exists(facade), $"{facade} was not packed");

        using var package = Packages.OpenPackage(facade);
        var nuspec = Packages.ReadNuspec(package, facade);

        var dependencies = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .ToList();

        // The .Mac dependency lives only in the macos dependency group, so it is present only when
        // the macos leg was packed (a macOS runner) — assert its presence and its exact pin there.
        var macDependencies = dependencies
            .Where(d => d.Attribute("id")?.Value == mac)
            .ToList();
        Skip.If(macDependencies.Count == 0, $"{facade} was packed without its macOS leg (non-macOS runner)");

        // Exact pin, not a floating range — same reasoning as the Android/iOS pins.
        foreach (var dependency in macDependencies)
        {
            var version = dependency.Attribute("version")?.Value;
            Assert.True(
                version is not null && version.StartsWith('[') && version.EndsWith(']'),
                $"{mac} dependency version '{version}' is not an exact pin ([x.y.z]).");
        }
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
        string facade, string? maui, string android, string ios)
    {
        _ = (android, ios);

        Skip.If(maui is null, $"{facade} has no MAUI companion by design");
        Skip.IfNot(Packages.Exists(maui!), $"{maui} was not packed");

        using var package = Packages.OpenPackage(maui!);
        var nuspec = Packages.ReadNuspec(package, maui!);

        var ids = nuspec.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => e.Attribute("id")?.Value)
            .ToHashSet();

        Assert.Contains(facade, ids);
    }
}
