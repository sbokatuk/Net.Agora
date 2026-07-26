using System.IO.Compression;
using System.Xml.Linq;

namespace Net.Agora.PackageTests;

/// <summary>
/// Locates the packed .nupkg files and describes what each package is expected to contain.
///
/// This repository packs a pure façade over Net.Agora.Video.Android / Net.Agora.Video.iOS —
/// packages published from sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS respectively.
/// Their own package shape (native .aar/xcframework payload, their own target frameworks) is
/// asserted by those repositories' own package tests, not here; what this repository is
/// responsible for is that its own packages (Net.Agora.Video, Net.Agora.Video.Maui) build for the
/// right target frameworks and depend on the platform packages at the pinned version.
/// </summary>
public static class Packages
{
    public const string VideoAndroid = "Net.Agora.Video.Android";
    public const string VideoIOS = "Net.Agora.Video.iOS";
    public const string VideoMac = "Net.Agora.Video.Mac";
    public const string Video = "Net.Agora.Video";
    public const string VideoMaui = "Net.Agora.Video.Maui";
    public const string VoiceAndroid = "Net.Agora.Voice.Android";
    public const string VoiceIOS = "Net.Agora.Voice.iOS";
    public const string VoiceMac = "Net.Agora.Voice.Mac";
    public const string Voice = "Net.Agora.Voice";
    public const string VoiceMaui = "Net.Agora.Voice.Maui";
    public const string SignalingAndroid = "Net.Agora.Signaling.Android";
    public const string SignalingIOS = "Net.Agora.Signaling.iOS";
    public const string SignalingMac = "Net.Agora.Signaling.Mac";
    public const string Signaling = "Net.Agora.Signaling";
    public const string SignalingMaui = "Net.Agora.Signaling.Maui";
    public const string ChatAndroid = "Net.Agora.Chat.Android";
    public const string ChatIOS = "Net.Agora.Chat.iOS";
    public const string Chat = "Net.Agora.Chat";
    public const string ChatMaui = "Net.Agora.Chat.Maui";
    public const string WhiteboardAndroid = "Net.Agora.Whiteboard.Android";
    public const string WhiteboardIOS = "Net.Agora.Whiteboard.iOS";
    public const string Whiteboard = "Net.Agora.Whiteboard";
    public const string WhiteboardMaui = "Net.Agora.Whiteboard.Maui";
    public const string FastboardAndroid = "Net.Agora.Fastboard.Android";
    public const string FastboardIOS = "Net.Agora.Fastboard.iOS";
    public const string Fastboard = "Net.Agora.Fastboard";
    public const string FastboardMaui = "Net.Agora.Fastboard.Maui";

    /// <summary>
    /// One row per product this repository packs: the façade package, its MAUI companion, and the
    /// two platform packages the façade must pin. Pinned rather than discovered so a product
    /// silently dropped from the pack is a failure, not something the tests adapt to.
    /// </summary>
    /// <remarks>
    /// The <c>Mac</c> column is the native macOS (AppKit) platform package, or null for a product
    /// Agora ships no macOS SDK for. Only Video, Voice and Signaling have a macOS leg — see
    /// sbokatuk/Net.Agora.Mac; Chat/Whiteboard/Fastboard have no <c>.Mac</c> package. Note the macOS
    /// leg has no <c>.Maui</c> companion: MAUI's only desktop-Mac target is Mac Catalyst, which
    /// Agora ships no slice for.
    /// </remarks>
    public static readonly (string Facade, string? Maui, string Android, string Ios, string? Mac)[] Products =
    [
        (Video, VideoMaui, VideoAndroid, VideoIOS, VideoMac),
        (Voice, VoiceMaui, VoiceAndroid, VoiceIOS, VoiceMac),
        // Its .Maui companion adds nothing platform-specific — RTM needs no Android Context — and
        // exists only for the CreateClient() symmetry the other products have; see
        // src/Net.Agora.Signaling.Maui. It is still packed and pinned, so it belongs in this table.
        (Signaling, SignalingMaui, SignalingAndroid, SignalingIOS, SignalingMac),
        (Chat, ChatMaui, ChatAndroid, ChatIOS, null),
        // The one product whose two platform packages are on different version lines — netless
        // releases the Android and iOS whiteboards from separate repositories.
        (Whiteboard, WhiteboardMaui, WhiteboardAndroid, WhiteboardIOS, null),
        (Fastboard, FastboardMaui, FastboardAndroid, FastboardIOS, null),
    ];

    public static IEnumerable<object[]> ProductRows =>
        Products.Select(p => new object[] { p.Facade, p.Maui!, p.Android, p.Ios });

    /// <summary>Every (façade, macOS platform package) pair, for the products that have a macOS leg.</summary>
    public static IEnumerable<object[]> MacProductRows =>
        Products.Where(p => p.Mac is not null).Select(p => new object[] { p.Facade, p.Mac! });

    /// <summary>
    /// Target frameworks Net.Agora.Video / Net.Agora.Video.Maui must carry, one per SDK band pass.
    /// Pinned rather than discovered: a package that silently lost a target framework because a
    /// pack pass failed is exactly the regression these tests exist to catch.
    /// </summary>
    public static readonly string[] AndroidTargetFrameworks =
    [
        "net8.0-android34.0", "net9.0-android35.0",
    ];

    /// <inheritdoc cref="AndroidTargetFrameworks" />
    public static readonly string[] IosTargetFrameworks =
    [
        "net8.0-ios18.0", "net9.0-ios18.0",
    ];

    /// <summary>
    /// The macOS target frameworks the façades with a macOS leg carry. The macOS workload appends
    /// its platform version to the folder name (net8.0-macos15.0, not a bare net8.0-macos), so the
    /// exact strings the pack produces are pinned here — the same shape as the .Mac binding repo.
    /// </summary>
    public static readonly string[] MacosTargetFrameworks =
    [
        "net8.0-macos15.0", "net9.0-macos15.0",
    ];

    public static IEnumerable<object[]> AndroidFrameworks =>
        AndroidTargetFrameworks.Select(tfm => new object[] { tfm });

    public static IEnumerable<object[]> IosFrameworks =>
        IosTargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>Every (façade package, target framework) pair, per platform axis.</summary>
    public static IEnumerable<object[]> FacadeAndroidFrameworks =>
        Products.SelectMany(p => AndroidTargetFrameworks.Select(tfm => new object[] { p.Facade, tfm }));

    /// <inheritdoc cref="FacadeAndroidFrameworks" />
    public static IEnumerable<object[]> FacadeIosFrameworks =>
        Products.SelectMany(p => IosTargetFrameworks.Select(tfm => new object[] { p.Facade, tfm }));

    /// <summary>Every (façade, macOS target framework) pair, for the products that have a macOS leg.</summary>
    public static IEnumerable<object[]> FacadeMacosFrameworks =>
        Products.Where(p => p.Mac is not null)
            .SelectMany(p => MacosTargetFrameworks.Select(tfm => new object[] { p.Facade, tfm }));

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    /// <summary>
    /// The Apple leg is only built on macOS, so on a Linux run the iOS-specific checks skip
    /// rather than fail. CI validates on a runner that has both sets downloaded.
    /// </summary>
    public static bool Exists(string packageId) => Find(packageId, throwIfMissing: false) is not null;

    public static string FindPackage(string packageId, string extension = ".nupkg") =>
        Find(packageId, throwIfMissing: true, extension)!;

    private static string? Find(string packageId, bool throwIfMissing, string extension = ".nupkg")
    {
        // The id is a filename prefix, and Net.Agora.Video is a prefix of Net.Agora.Video.Android,
        // so the version must be matched too or the metapackage lookup would find both.
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{packageId}.*{extension}")
                .Where(f => IsVersionOf(packageId, Path.GetFileName(f), extension))
                .ToArray()
            : [];

        if (matches.Length == 0)
        {
            Assert.True(
                !throwIfMissing,
                $"No {packageId}.<version>{extension} found in '{ArtifactsDirectory}'. " +
                "Run build/BuildNugets.sh (or the CI pack step) first.");
            return null;
        }

        // A rebuilt working copy can leave several versions behind; test the newest.
        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    /// <summary>
    /// True when <paramref name="fileName" /> is "&lt;packageId&gt;.&lt;version&gt;&lt;extension&gt;"
    /// — that is, the remainder after the id starts a version rather than another id segment.
    /// </summary>
    private static bool IsVersionOf(string packageId, string fileName, string extension)
    {
        var remainder = fileName[(packageId.Length + 1)..^extension.Length];
        return remainder.Length > 0 && char.IsDigit(remainder[0]);
    }

    public static ZipArchive OpenPackage(string packageId, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(packageId, extension));

    public static XDocument ReadNuspec(ZipArchive package, string packageId)
    {
        var entry = package.GetEntry($"{packageId}.nuspec");
        Assert.True(entry is not null, $"{packageId} has no .nuspec entry.");

        using var stream = entry!.Open();
        return XDocument.Load(stream);
    }

    private static string ResolveArtifactsDirectory()
    {
        // Walk up to the repository root (the directory holding global.json).
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;

        // A relative AGORA_ARTIFACTS is resolved against the repository root, not the current
        // directory. The test process runs from bin/<config>/<tfm>, so the obvious-looking
        // AGORA_ARTIFACTS=artifacts would otherwise point at a directory inside the build output
        // and report every package as missing.
        return Environment.GetEnvironmentVariable("AGORA_ARTIFACTS") is { Length: > 0 } configured
            ? Path.GetFullPath(configured, root)
            : Path.Combine(root, "artifacts");
    }
}
