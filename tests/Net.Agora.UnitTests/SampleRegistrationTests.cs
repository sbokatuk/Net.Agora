namespace Net.Agora;

/// <summary>
/// Guards the one wiring step a MAUI sample cannot get wrong silently: a sample whose UI uses one
/// of the Agora views must call the matching <c>UseAgora*()</c> builder extension in its
/// <c>MauiProgram</c>, or the view never receives a platform view and <c>CreateClient</c> throws
/// at runtime.
/// </summary>
/// <remarks>
/// CI builds the samples but never launches them, so a missing registration compiles clean and
/// fails only in someone's hands — exactly what happened to the Whiteboard and Fastboard samples,
/// which shipped with a comment copy-pasted from the Chat sample claiming there was nothing to
/// register. This test derives the requirement from the sample sources themselves (any file
/// mentioning the view type) rather than a hard-coded sample list, so a future sample is covered
/// the day it is added.
/// </remarks>
public class SampleRegistrationTests
{
    /// <summary>View type → the builder call that registers its handler.</summary>
    private static readonly (string View, string Registration)[] Registrations =
    [
        ("AgoraVideoView", "UseAgoraVideo"),
        ("AgoraWhiteboardView", "UseAgoraWhiteboard"),
        ("AgoraFastboardView", "UseAgoraFastboard"),
    ];

    public static TheoryData<string> MauiSampleDirectories()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(RepositoryRoot(), "samples")))
        {
            // Native (AppKit) samples have no MauiProgram and register nothing.
            if (File.Exists(Path.Combine(dir, "MauiProgram.cs")))
                data.Add(Path.GetFileName(dir));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MauiSampleDirectories))]
    public void Sample_registers_the_handler_for_every_agora_view_it_uses(string sampleName)
    {
        var sampleDir = Path.Combine(RepositoryRoot(), "samples", sampleName);
        var mauiProgram = File.ReadAllText(Path.Combine(sampleDir, "MauiProgram.cs"));
        var sources = Directory
            .EnumerateFiles(sampleDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xaml", StringComparison.Ordinal)
                     || f.EndsWith(".cs", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToList();

        foreach (var (view, registration) in Registrations)
        {
            if (sources.Any(source => source.Contains(view, StringComparison.Ordinal)))
                Assert.True(
                    mauiProgram.Contains(registration, StringComparison.Ordinal),
                    $"{sampleName} uses {view} but its MauiProgram.cs never calls .{registration}(); " +
                    "the view would have no handler and CreateClient would throw at runtime.");
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Net.Agora.sln")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Net.Agora.sln not found above the test output directory.");
    }
}
