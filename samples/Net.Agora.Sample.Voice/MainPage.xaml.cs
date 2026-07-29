using System.Text;
using Net.Agora.Voice;
using Net.Agora.Voice.Maui;

namespace Net.Agora.Sample.Voice;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's microphone and showing who is speaking.
/// The whole app is this one file — there is no per-platform code, because
/// <c>Net.Agora.Voice</c> presents the same client on Android and iOS and
/// <c>Net.Agora.Voice.Maui</c> supplies the Android <c>Context</c>.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();
    private AgoraVoiceClient? _client;

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

        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            Append("microphone permission denied");
            return;
        }

        SetBusy(true);

        AgoraVoiceClient? client = null;
        try
        {
            // Built here rather than in the constructor: on Android CreateClient needs the
            // current activity, which does not exist until the page is on screen.
            var options = new AgoraVoiceOptions
            {
                AppId = appId,
                Token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text.Trim(),
                DefaultToSpeakerphone = SpeakerSwitch.IsToggled,
            };

            client = options.CreateClient();

            // The SDK raises callbacks on its own thread, so anything touching the UI hops back.
            client.Joined += (_, ev) => Append($"joined {ev.ChannelId} as {ev.Uid}");
            client.Left += (_, ev) => Append($"left {ev.ChannelId}");
            client.UserJoined += (_, ev) => Append($"remote user joined: {ev.Uid}");
            client.UserOffline += (_, ev) => Append($"remote user left: {ev.Uid}");
            client.RemoteAudioMuted += (_, ev) => Append($"user {ev.Uid} {(ev.Muted ? "muted" : "unmuted")}");
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
            client.ConnectionStateChanged += (_, ev) => Append($"connection: {ev.State} (reason {ev.Reason})");
            client.TokenPrivilegeWillExpire += (_, _) =>
                Append("token expires soon — fetch a fresh one and call RenewToken");
            client.Error += (_, ev) => Append($"error {ev.ErrorCode}: {ev.Message}");

            // Who-is-speaking reports every 200 ms — drives the label above.
            client.EnableVolumeIndication(TimeSpan.FromMilliseconds(200));

            // Completes when the server confirms, so there is no callback to wire up for the
            // common case, and a failure to join surfaces as an exception right here.
            await client.JoinAsync(channelId);

            _client = client;
            SetJoined(true);
        }
        catch (Exception exception) when (exception is AgoraVoiceException or InvalidOperationException)
        {
            // The RTC engine is a process-wide singleton (see AgoraVoiceClient's constructor
            // guard): a client that fails to join must be disposed here, or it keeps holding the
            // engine and every later Join attempt throws InvalidOperationException instead of
            // getting a clean retry. That guard is not an AgoraVoiceException, so it has to be
            // caught here too — letting it through crashed the app on a second Join tap.
            Append($"failed to join: {exception.Message}");
            client?.Dispose();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        // Dispose, not just Leave: the RTC engine is a process-wide singleton (see
        // AgoraEngineSlot), released only on Dispose. OnJoinClicked always builds a fresh client,
        // so leaving without disposing the old one means the next Join finds the slot still taken
        // and throws — a rejoin after Leave would look broken from a bug here, not the engine.
        _client?.Dispose();
        _client = null;
        Append("left");
        SetJoined(false);
    }

    private void OnMuteToggled(object sender, ToggledEventArgs e)
    {
        _client?.MuteLocalAudio(e.Value);
        Append(e.Value ? "microphone muted" : "microphone unmuted");
    }

    private void OnSpeakerToggled(object sender, ToggledEventArgs e)
    {
        // Before a join this sets the default route (and is also captured into the options at
        // join time); during a call it switches the live route.
        _client?.SetSpeakerphone(e.Value);
        Append(e.Value ? "audio routed to speaker" : "audio routed to earpiece");
    }

    private void SetBusy(bool busy) => JoinButton.IsEnabled = !busy;

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        MuteSwitch.IsEnabled = joined;
        if (!joined)
        {
            MuteSwitch.IsToggled = false;
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

        // Releases the microphone and the native engine when the page goes away.
        if (Handler is null)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
