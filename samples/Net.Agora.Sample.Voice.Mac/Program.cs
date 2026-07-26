using AppKit;
using CoreGraphics;
using Foundation;
using Net.Agora.Voice;

namespace Net.Agora.Sample.Voice.Mac;

public static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;
        app.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        app.Delegate = new AppDelegate();
        app.Run();
    }
}

/// <summary>
/// A one-window AppKit app driving the cross-platform <see cref="AgoraVoiceClient"/> on macOS: join
/// an audio channel, mute/unmute the microphone, and see who is speaking. No camera anywhere.
/// </summary>
public sealed class AppDelegate : NSApplicationDelegate
{
    private NSWindow _window = null!;
    private NSTextField _appId = null!;
    private NSTextField _channel = null!;
    private NSTextField _status = null!;
    private NSButton _mute = null!;

    private AgoraVoiceClient? _client;
    private bool _muted;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _window = new NSWindow(
            new CGRect(0, 0, 620, 220),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable,
            NSBackingStore.Buffered,
            deferCreation: false)
        {
            Title = "Net.Agora Voice (macOS)",
        };
        _window.Center();

        var content = _window.ContentView!;
        _appId = LabeledField(content, "App ID", 170, "your Agora App ID");
        _channel = LabeledField(content, "Channel", 130, "channel name");
        _channel.StringValue = "demo";

        var join = new NSButton { Title = "Join", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(20, 80, 90, 30) };
        join.Activated += async (_, _) => await JoinAsync();
        content.AddSubview(join);

        _mute = new NSButton { Title = "Mute", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(120, 80, 90, 30), Enabled = false };
        _mute.Activated += (_, _) => ToggleMute();
        content.AddSubview(_mute);

        var leave = new NSButton { Title = "Leave", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(220, 80, 90, 30) };
        leave.Activated += (_, _) => Leave();
        content.AddSubview(leave);

        _status = new NSTextField(new CGRect(20, 30, 580, 20))
        {
            Editable = false, Bezeled = false, DrawsBackground = false, StringValue = "Not joined.",
        };
        content.AddSubview(_status);

        _window.MakeKeyAndOrderFront(null);
#pragma warning disable CA1422 // see the Video.Mac sample.
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
#pragma warning restore CA1422
    }

    private async Task JoinAsync()
    {
        if (_client is not null)
        {
            return;
        }

        var appId = _appId.StringValue.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            _status.StringValue = "Enter an App ID first.";
            return;
        }

        _client = new AgoraVoiceClient(new AgoraVoiceOptions { AppId = appId });
        _client.VolumeIndication += (_, e) => OnMain(() =>
        {
            if (e.Speakers.Count == 0)
            {
                _status.StringValue = "Joined — silence.";
                return;
            }

            var loudest = e.Speakers.OrderByDescending(s => s.Volume).First();
            _status.StringValue = $"Loudest: uid {loudest.Uid} @ {loudest.Volume}";
        });
        _client.Error += (_, e) => OnMain(() => _status.StringValue = $"Error: {e.Message}");

        try
        {
            _status.StringValue = "Joining…";
            await _client.JoinAsync(_channel.StringValue.Trim());
            _client.EnableVolumeIndication(TimeSpan.FromMilliseconds(400));
            _mute.Enabled = true;
            _status.StringValue = $"Joined '{_channel.StringValue.Trim()}' as {_client.LocalUid}.";
        }
        catch (Exception ex)
        {
            _status.StringValue = $"Join failed: {ex.Message}";
        }
    }

    private void ToggleMute()
    {
        if (_client is null)
        {
            return;
        }

        _muted = !_muted;
        _client.MuteLocalAudio(_muted);
        _mute.Title = _muted ? "Unmute" : "Mute";
    }

    private void Leave()
    {
        if (_client is null)
        {
            return;
        }

        _client.Leave();
        _client.Dispose();
        _client = null;
        _muted = false;
        _mute.Enabled = false;
        _mute.Title = "Mute";
        _status.StringValue = "Left the channel.";
    }

    private static void OnMain(Action action) =>
        NSApplication.SharedApplication.BeginInvokeOnMainThread(action);

    private static NSTextField LabeledField(NSView parent, string label, nfloat y, string placeholder)
    {
        parent.AddSubview(new NSTextField(new CGRect(20, y, 70, 20))
        {
            StringValue = label, Editable = false, Bezeled = false, DrawsBackground = false,
        });
        var field = new NSTextField(new CGRect(95, y - 2, 300, 24)) { PlaceholderString = placeholder };
        parent.AddSubview(field);
        return field;
    }
}
