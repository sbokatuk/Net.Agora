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
    private bool _previewing;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        // The "check your hair" flow: local camera into the local view before any join —
        // StartPreview needs no channel, no token and no network, just the camera.
        if (_previewing)
        {
            _client?.StopPreview();
            _previewing = false;
            PreviewButton.Text = "Preview";
            Append("preview stopped");
            return;
        }

        if (!await RequestCapturePermissionsAsync())
        {
            Append("camera and microphone permission denied");
            return;
        }

        var client = EnsureClient();
        if (client is null)
        {
            return;
        }

        client.EnableVideo();
        client.SetLocalView(LocalView);
        client.StartPreview();
        _previewing = true;
        PreviewButton.Text = "Stop preview";
        SwitchCameraButton.IsEnabled = true;
        Append("previewing — Flip switches the camera");
    }

    /// <summary>
    /// One client for preview and join alike, created on first use (on Android it needs the
    /// current activity, and the video views need their handlers — neither exists until the page
    /// is on screen). Leave disposes it, so a fresh App ID takes effect on the next attempt.
    /// </summary>
    private AgoraVideoClient? EnsureClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        var appId = AppIdEntry.Text?.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return null;
        }

        var options = new AgoraVideoOptions
        {
            AppId = appId,
            Token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text.Trim(),
        };

        var client = options.CreateClient();
        WireEvents(client);
        _client = client;
        return client;
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        var channelId = ChannelIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(channelId))
        {
            Append("enter a channel id first");
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
            var client = EnsureClient();
            if (client is null)
            {
                return;
            }

            client.SetLocalView(LocalView);

            client.EnableVideo();
            client.SetSpeakerphone(SpeakerSwitch.IsToggled);

            // Who-is-speaking reports every 200 ms — drives the label under the controls.
            client.EnableVolumeIndication(TimeSpan.FromMilliseconds(200));

            // Completes when the server confirms, so there is no callback to wire up for the
            // common case, and a failure to join surfaces as an exception right here.
            await client.JoinAsync(channelId);

            _client = client;
            _previewing = false;
            PreviewButton.Text = "Preview";
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

    /// <summary>The SDK raises callbacks on its own thread, so anything touching the UI hops back.</summary>
    private void WireEvents(AgoraVideoClient client)
    {
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
            client.RemoteAudioMuted += (_, ev) => Append($"user {ev.Uid} {(ev.Muted ? "muted" : "unmuted")} their mic");
            client.RemoteVideoMuted += (_, ev) => Append($"user {ev.Uid} {(ev.Muted ? "paused" : "resumed")} their camera");
            client.ConnectionStateChanged += (_, ev) => Append($"connection: {ev.State} (reason {ev.Reason})");
            client.TokenPrivilegeWillExpire += (_, _) =>
                Append("token expires soon — fetch a fresh one and call RenewToken");
            client.VolumeIndication += (_, ev) => MainThread.BeginInvokeOnMainThread(() =>
            {
                var speaking = ev.Speakers
                    .Where(s => s.Volume > 0)
                    .Select(s => s.Uid == 0 ? $"you ({s.Volume})" : $"{s.Uid} ({s.Volume})")
                    .ToList();
                SpeakersLabel.Text = speaking.Count > 0
                    ? $"speaking: {string.Join(", ", speaking)}"
                    : "nobody is speaking";
            });
            client.Error += (_, ev) => Append($"error {ev.ErrorCode}: {ev.Message}");
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        // Dispose rather than just Leave: the sample lets you change the App ID between runs,
        // and the engine is created with one — a fresh client picks the new value up.
        _client?.Leave();
        _client?.Dispose();
        _client = null;
        _previewing = false;
        PreviewButton.Text = "Preview";
        Append("left");
        SetJoined(false);
    }

    private void OnSwitchCameraClicked(object sender, EventArgs e) => _client?.SwitchCamera();

    private void OnMuteToggled(object sender, ToggledEventArgs e)
    {
        _client?.MuteLocalAudio(e.Value);
        Append(e.Value ? "microphone muted" : "microphone unmuted");
    }

    private void OnCameraOffToggled(object sender, ToggledEventArgs e)
    {
        _client?.MuteLocalVideo(e.Value);
        Append(e.Value ? "camera paused" : "camera resumed");
    }

    private void OnSpeakerToggled(object sender, ToggledEventArgs e)
    {
        // Before a join this sets the default route; during a call it switches the live route.
        _client?.SetSpeakerphone(e.Value);
        Append(e.Value ? "audio routed to speaker" : "audio routed to earpiece");
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
        PreviewButton.IsEnabled = !joined;
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        SwitchCameraButton.IsEnabled = joined;
        MuteSwitch.IsEnabled = joined;
        CameraOffSwitch.IsEnabled = joined;
        if (!joined)
        {
            MuteSwitch.IsToggled = false;
            CameraOffSwitch.IsToggled = false;
            SpeakersLabel.Text = "nobody is speaking";
        }
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
