using AppKit;
using CoreGraphics;
using Foundation;
using Net.Agora.Video;

namespace Net.Agora.Sample.Video.Mac;

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
/// A one-window AppKit app driving the <b>cross-platform</b> <see cref="AgoraVideoClient"/> on
/// macOS — the same API an Android or iOS app uses, here over the native macOS SDK via
/// Net.Agora.Video's net-macos leg. Enter an App ID and channel, press Join: the local camera
/// renders on the left and the first remote user on the right.
/// </summary>
public sealed class AppDelegate : NSApplicationDelegate
{
    private NSWindow _window = null!;
    private NSTextField _appId = null!;
    private NSTextField _channel = null!;
    private NSTextField _token = null!;
    private NSView _localView = null!;
    private NSView _remoteView = null!;
    private NSTextField _status = null!;

    private AgoraVideoClient? _client;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _window = new NSWindow(
            new CGRect(0, 0, 900, 600),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            deferCreation: false)
        {
            Title = "Net.Agora Video (macOS)",
        };
        _window.Center();

        var content = _window.ContentView!;
        _appId = LabeledField(content, "App ID", 560, "your Agora App ID");
        _channel = LabeledField(content, "Channel", 520, "channel name");
        _channel.StringValue = "demo";
        _token = LabeledField(content, "Token", 480, "token (optional — App ID-only auth is testing-only)");

        var join = new NSButton { Title = "Join", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(20, 440, 90, 30) };
        join.Activated += async (_, _) => await JoinAsync();
        content.AddSubview(join);

        var leave = new NSButton { Title = "Leave", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(120, 440, 90, 30) };
        leave.Activated += (_, _) => Leave();
        content.AddSubview(leave);

        _status = new NSTextField(new CGRect(230, 445, 640, 20))
        {
            Editable = false, Bezeled = false, DrawsBackground = false, StringValue = "Not joined.",
        };
        content.AddSubview(_status);

        _localView = VideoPane(content, new CGRect(20, 20, 420, 400), "Local");
        _remoteView = VideoPane(content, new CGRect(460, 20, 420, 400), "Remote");

        _window.MakeKeyAndOrderFront(null);
#pragma warning disable CA1422 // ActivateIgnoringOtherApps is obsolete from macOS 14; its
                               // replacement does not exist below this app's SupportedOSPlatformVersion.
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

        _client = new AgoraVideoClient(new AgoraVideoOptions
        {
            AppId = appId,
            Token = string.IsNullOrWhiteSpace(_token.StringValue) ? null : _token.StringValue.Trim(),
            ChannelProfile = AgoraChannelProfile.LiveBroadcasting,
            ClientRole = AgoraClientRole.Broadcaster,
        });

        _client.UserJoined += (_, e) => OnMain(() =>
        {
            _client!.SetRemoteView(e.Uid, _remoteView);
            _status.StringValue = $"Remote user {e.Uid} joined.";
        });
        _client.UserOffline += (_, e) => OnMain(() => _status.StringValue = $"Remote user {e.Uid} left.");
        _client.Error += (_, e) => OnMain(() => _status.StringValue = $"Error: {e.Message}");

        _client.EnableVideo();
        _client.SetLocalView(_localView);
        _client.StartPreview();

        try
        {
            _status.StringValue = "Joining…";
            await _client.JoinAsync(_channel.StringValue.Trim());
            _status.StringValue = $"Joined '{_channel.StringValue.Trim()}' as {_client.LocalUid}.";
        }
        catch (Exception ex)
        {
            _status.StringValue = $"Join failed: {ex.Message}";
        }
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

    private static NSView VideoPane(NSView parent, CGRect frame, string label)
    {
        var box = new NSView(frame) { WantsLayer = true };
        box.Layer!.BackgroundColor = NSColor.Black.CGColor;
        parent.AddSubview(box);
        parent.AddSubview(new NSTextField(new CGRect(frame.X, frame.Y + frame.Height + 2, 200, 18))
        {
            StringValue = label, Editable = false, Bezeled = false, DrawsBackground = false,
        });
        return box;
    }
}
