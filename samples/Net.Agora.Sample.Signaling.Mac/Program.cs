using AppKit;
using CoreGraphics;
using Foundation;
using Net.Agora.Signaling;

namespace Net.Agora.Sample.Signaling.Mac;

public static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        var app = NSApplication.SharedApplication;
        app.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        app.MainMenu = BuildMainMenu();
        app.Delegate = new AppDelegate();
        app.Run();
    }

    // A bare NSApplication has no Edit menu, so Cmd+V/C/X/A have nothing to dispatch to — macOS
    // routes those key equivalents through the menu bar, not directly to the first responder.
    private static NSMenu BuildMainMenu()
    {
        var mainMenu = new NSMenu();

        var editMenuItem = new NSMenuItem();
        mainMenu.AddItem(editMenuItem);
        var editMenu = new NSMenu("Edit");
        editMenu.AddItem(new NSMenuItem("Cut", new ObjCRuntime.Selector("cut:"), "x"));
        editMenu.AddItem(new NSMenuItem("Copy", new ObjCRuntime.Selector("copy:"), "c"));
        editMenu.AddItem(new NSMenuItem("Paste", new ObjCRuntime.Selector("paste:"), "v"));
        editMenu.AddItem(new NSMenuItem("Select All", new ObjCRuntime.Selector("selectAll:"), "a"));
        editMenuItem.Submenu = editMenu;

        return mainMenu;
    }
}

/// <summary>
/// A one-window AppKit app driving the cross-platform <see cref="AgoraSignalingClient"/> on macOS:
/// log in, subscribe to a channel, and publish/receive messages — a tiny chat room.
/// </summary>
public sealed class AppDelegate : NSApplicationDelegate
{
    private NSWindow _window = null!;
    private NSTextField _appId = null!;
    private NSTextField _userId = null!;
    private NSTextField _channel = null!;
    private NSTextField _token = null!;
    private NSTextField _message = null!;
    private NSTextView _log = null!;

    private AgoraSignalingClient? _client;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _window = new NSWindow(
            new CGRect(0, 0, 640, 500),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            deferCreation: false)
        {
            Title = "Net.Agora Signaling (macOS)",
        };
        _window.Center();

        var content = _window.ContentView!;
        _appId = LabeledField(content, "App ID", 450, "your Agora App ID");
        _userId = LabeledField(content, "User", 410, "a user id");
        _userId.StringValue = "mac-user";
        _channel = LabeledField(content, "Channel", 370, "channel name");
        _channel.StringValue = "demo";
        _token = LabeledField(content, "Token", 330, "RTM token (leave empty if App Certificate is off)");

        var login = new NSButton { Title = "Login + Subscribe", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(20, 285, 160, 30) };
        login.Activated += async (_, _) => await LoginAsync();
        content.AddSubview(login);

        _message = new NSTextField(new CGRect(20, 245, 470, 24)) { PlaceholderString = "message" };
        content.AddSubview(_message);
        var send = new NSButton { Title = "Send", BezelStyle = NSBezelStyle.Rounded, Frame = new CGRect(500, 243, 90, 28) };
        send.Activated += async (_, _) => await SendAsync();
        content.AddSubview(send);

        var scroll = new NSScrollView(new CGRect(20, 20, 600, 210)) { HasVerticalScroller = true, BorderType = NSBorderType.BezelBorder };
        _log = new NSTextView(new CGRect(0, 0, 600, 210)) { Editable = false };
        scroll.DocumentView = _log;
        content.AddSubview(scroll);

        _window.MakeKeyAndOrderFront(null);
#pragma warning disable CA1422 // see the Video.Mac sample.
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
#pragma warning restore CA1422
    }

    private async Task LoginAsync()
    {
        if (_client is not null)
        {
            return;
        }

        var appId = _appId.StringValue.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            Append("Enter an App ID first.");
            return;
        }

        var token = _token.StringValue.Trim();
        _client = new AgoraSignalingClient(new AgoraSignalingOptions
        {
            AppId = appId,
            UserId = _userId.StringValue.Trim(),
            Token = string.IsNullOrEmpty(token) ? null : token,
        });
        _client.MessageReceived += (_, e) => OnMain(() => Append($"[{e.ChannelName}] {e.Publisher}: {e.Text}"));

        try
        {
            await _client.LoginAsync();
            await _client.SubscribeAsync(_channel.StringValue.Trim());
            Append($"Logged in as '{_userId.StringValue.Trim()}', subscribed to '{_channel.StringValue.Trim()}'.");
        }
        catch (Exception ex)
        {
            Append($"Login/subscribe failed: {ex.Message}");
            _client.Dispose();
            _client = null;
        }
    }

    private async Task SendAsync()
    {
        if (_client is null)
        {
            Append("Log in first.");
            return;
        }

        var text = _message.StringValue.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            await _client.PublishAsync(_channel.StringValue.Trim(), text);
            Append($"[me] {text}");
            _message.StringValue = string.Empty;
        }
        catch (Exception ex)
        {
            Append($"Publish failed: {ex.Message}");
        }
    }

    private void Append(string line) => _log.TextStorage!.Append(new NSAttributedString($"{line}\n"));

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
