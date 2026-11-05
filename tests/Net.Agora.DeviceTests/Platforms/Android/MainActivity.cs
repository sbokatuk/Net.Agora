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
        SmokeTests.Setting = ReadSettings();

        _ = TestRunner.RunAndReportAsync(
            message => Log.Info(LogTag, message),
            // Not exiting the process: the runner reads the verdict from logcat, and killing the
            // app here would race the log being flushed.
            _ => { });
    }

    /// <summary>
    /// Reads the settings run-emulator-tests.sh wrote into this app's private storage, one
    /// <c>NAME=value</c> per line, and deletes the file so they don't outlive the run. They arrive as
    /// a file because the script can send it over adb's stdin, where an intent extra would put each
    /// value on a command line.
    /// </summary>
    private Func<string, string?> ReadSettings()
    {
        var path = Path.Combine(FilesDir!.AbsolutePath, "live-settings.env");
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);

        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var split = line.IndexOf('=');
                if (split > 0)
                {
                    settings[line[..split]] = line[(split + 1)..];
                }
            }

            File.Delete(path);
        }

        return name => settings.TryGetValue(name, out var value) ? value : null;
    }
}
