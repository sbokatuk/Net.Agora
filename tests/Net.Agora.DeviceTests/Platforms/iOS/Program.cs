using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
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
    public AppDelegate()
    {
    }

    // UIApplication.Main creates this delegate on the Objective-C side, so the runtime has to wrap
    // an existing native object, which takes this constructor. The .NET 9 iOS SDK uses the dynamic
    // registrar for this interpreter build, and without it the app dies before the first check
    // with "does not have a constructor that takes one NativeHandle argument". The net8 and net10
    // builds register statically and never call it.
    private AppDelegate(NativeHandle handle)
        : base(handle)
    {
    }

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

        // run-simulator-tests.sh passes settings as SIMCTL_CHILD_<name>, which simctl hands to the
        // app's environment as <name>: they reach neither the build nor a command line.
        SmokeTests.Setting = Environment.GetEnvironmentVariable;

        // On the main thread deliberately: AgoraRtcEngineKit's delegate callbacks are dispatched on
        // the main queue, so a check that awaits one has to run from it too. Nothing here blocks
        // long enough to trip the watchdog.
        _ = TestRunner.RunAndReportAsync(
            Console.WriteLine,
            exitCode =>
            {
                Console.Out.Flush();
                Terminate(exitCode);
            });

        return true;
    }

    // _exit rather than Environment.Exit, which calls exit(3): from RTC 4.7.0 the Voice package's
    // AgoraffmpegExtension registers an exit handler that spins in aosl_module_unregister once an
    // engine has existed, so exit(3) never returns and simctl launch --console-pty waits for ever.
    // _exit skips the handlers. Nothing is left to flush by then: the verdict is already written.
    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "_exit")]
    private static extern void Terminate(int status);
}
