using System.Text;
using Net.Agora.Video;
using Net.Agora.Video.Maui;

namespace Net.Agora.Sample;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's camera/microphone and rendering the first
/// remote user's video. The whole app is this one file — there is no per-platform code, because
/// <c>Net.Agora.Video</c> presents the same client on Android and iOS and
/// <c>Net.Agora.Video.Maui</c> supplies the video view and the Android <c>Context</c>.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();
    private AgoraVideoClient? _client;
    private uint? _remoteUid;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var channelId = ChannelIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(channelId))
        {
            Append("enter an App ID and a channel id first");
            return;
        }

        if (!await RequestCapturePermissionsAsync())
        {
            Append("camera and microphone permission denied");
            return;
        }

        SetBusy(true);

        try
        {
            // Built here rather than in the constructor: on Android CreateClient needs the
            // current activity, and the video views need their handlers, neither of which exists
            // until the page is on screen.
            var options = new AgoraVideoOptions
            {
                AppId = appId,
                Token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text.Trim(),
            };

            var client = options.CreateClient();
            client.SetLocalView(LocalView);

            // The SDK raises callbacks on its own thread, so anything touching the UI hops back.
            client.Joined += (_, ev) => Append($"joined {ev.ChannelId} as {ev.Uid}");
            client.Left += (_, ev) => Append($"left {ev.ChannelId}");
            client.UserJoined += (_, ev) => MainThread.BeginInvokeOnMainThread(() =>
            {
                Append($"remote user joined: {ev.Uid}");

                // Only one remote view in this sample — the first remote user gets it.
                if (_remoteUid is null)
                {
                    _remoteUid = ev.Uid;
                    client.SetRemoteView(ev.Uid, RemoteView);
                }
            });
            client.UserOffline += (_, ev) =>
            {
                Append($"remote user left: {ev.Uid}");
                if (_remoteUid == ev.Uid)
                {
                    _remoteUid = null;
                }
            };
            client.Error += (_, ev) => Append($"error {ev.ErrorCode}: {ev.Message}");

            client.EnableVideo();

            // Completes when the server confirms, so there is no callback to wire up for the
            // common case, and a failure to join surfaces as an exception right here.
            await client.JoinAsync(channelId);

            _client = client;
            SetJoined(true);
        }
        catch (AgoraVideoException exception)
        {
            Append($"failed to join: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        _client?.Leave();
        Append("left");
        SetJoined(false);
    }

    private static async Task<bool> RequestCapturePermissionsAsync()
    {
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();

        return camera == PermissionStatus.Granted && microphone == PermissionStatus.Granted;
    }

    private void SetBusy(bool busy) => JoinButton.IsEnabled = !busy;

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _status.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        StatusLabel.Text = _status.ToString();
        StatusScroll.ScrollToAsync(0, StatusLabel.Height, animated: false);
    });

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Releases the camera and the native renderers when the page goes away; the engine holds
        // a GL context that managed collection alone does not reclaim.
        if (Handler is null)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
