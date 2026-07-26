using AppKit;
using Foundation;

namespace Net.Agora.DeviceTests;

/// <summary>
/// Host for the on-host checks on macOS (AppKit). Runs every check on launch, reports the outcome
/// to stdout — which the runner script captures straight from the process, there being no simulator
/// to stream from — and exits with the verdict line the runner greps for.
/// </summary>
/// <remarks>
/// The macOS head exists because the Video, Voice and Signaling façades have a native macOS leg and
/// nothing else exercises it at runtime: the sample build proves the leg links, but only these
/// checks prove the packaged <c>Net.Agora.*.Mac</c> payload loads and the client drives it. The
/// remaining products have no macOS binding at all — see the csproj.
/// </remarks>
public static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;

        // No dock icon and no menu bar: this is a headless run driven from a script, not an app
        // anyone interacts with. Prohibited rather than Accessory because even an accessory app
        // takes a slot in the Dock's application list on a developer's machine.
        app.ActivationPolicy = NSApplicationActivationPolicy.Prohibited;
        app.Delegate = new AppDelegate();
        app.Run();
    }
}

public sealed class AppDelegate : NSApplicationDelegate
{
    public override void DidFinishLaunching(NSNotification notification)
    {
        // Started from the main thread deliberately, and not awaited: AgoraRtcEngineKit dispatches
        // its delegate callbacks on the main queue, so a check that awaits one has to be driven
        // from a thread that keeps pumping. Awaiting here would block the run loop the completions
        // arrive on and every awaiting check would time out.
        _ = TestRunner.RunAndReportAsync(
            Console.WriteLine,
            exitCode =>
            {
                Console.Out.Flush();

                // Terminate rather than return: nothing would otherwise end the AppKit run loop,
                // and the runner would hang until its own timeout instead of reading the verdict.
                Environment.Exit(exitCode);
            });
    }
}
