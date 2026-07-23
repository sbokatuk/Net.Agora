using Android.App;
using Android.OS;
using Android.Util;

namespace Net.Agora.DeviceTests;

/// <summary>
/// Host for the on-emulator checks. Runs every one on create and reports the outcome to logcat
/// under a single tag, which the runner script turns into an exit code.
/// </summary>
/// <remarks>
/// The activity name is pinned rather than left to the generated <c>crc64*</c> name, so
/// <c>adb shell am start</c> has a stable target across builds.
/// </remarks>
[Activity(Name = "com.sbokatuk.agora.devicetests.MainActivity", Label = "Net.Agora e2e", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    private const string LogTag = "AgoraE2E";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set before the checks run, including the very first one: AgoraVideoClient's Android
        // constructor needs a Context to create the engine, and this activity is the only one this
        // process ever has.
        SmokeTests.AndroidContext = this;

        _ = TestRunner.RunAndReportAsync(
            message => Log.Info(LogTag, message),
            // Not exiting the process: the runner reads the verdict from logcat, and killing the
            // app here would race the log being flushed.
            _ => { });
    }
}
