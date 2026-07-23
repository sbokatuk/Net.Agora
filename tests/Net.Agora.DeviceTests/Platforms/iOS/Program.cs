using Foundation;
using UIKit;

namespace Net.Agora.DeviceTests;

/// <summary>
/// Host for the on-simulator checks. Runs every one on launch, reports the outcome to stdout -
/// which <c>simctl launch --console-pty</c> streams straight back to CI - and exits with a verdict
/// line the runner script greps for.
/// </summary>
public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register(nameof(AppDelegate))]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // A window is not strictly needed for a headless run, but iOS terminates an app that never
        // presents one, which would look like a crash rather than a failing check.
        var root = new UIViewController();
        root.View!.BackgroundColor = UIColor.White;

#pragma warning disable CA1422 // UIScreen.MainScreen / this UIWindow constructor are obsolete from
                               // iOS 26 in favour of UIWindowScene overloads that do not exist below
                               // iOS 13 - this app's own SupportedOSPlatformVersion.
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = root };
#pragma warning restore CA1422
        Window.MakeKeyAndVisible();

        // On the main thread deliberately: AgoraRtcEngineKit's delegate callbacks are dispatched on
        // the main queue, so a check that awaits one has to run from it too. Nothing here blocks
        // long enough to trip the watchdog.
        _ = TestRunner.RunAndReportAsync(
            Console.WriteLine,
            exitCode => Environment.Exit(exitCode));

        return true;
    }
}
