using System.Text;
using Net.Agora.Signaling;
using Net.Agora.Signaling.Maui;

namespace Net.Agora.Sample.Signaling;

/// <summary>
/// A tiny chat room over Agora Signaling: log in, subscribe to one channel, publish string
/// messages and show what arrives. The whole app is this one file — there is no per-platform
/// code, because <c>Net.Agora.Signaling</c> presents the same client on Android and iOS and
/// needs no platform glue at all. It constructs the client through
/// <c>Net.Agora.Signaling.Maui</c>'s <c>CreateClient()</c> only for symmetry with the other
/// products' samples.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private AgoraSignalingClient? _client;
    private string? _channel;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var userId = UserIdEntry.Text?.Trim();
        var channel = ChannelEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(channel))
        {
            Append("enter an App ID, a user id and a channel first");
            return;
        }

        LoginButton.IsEnabled = false;

        try
        {
            // CreateClient() from Net.Agora.Signaling.Maui — the same call every other product's
            // sample makes. For Signaling it is exactly `new AgoraSignalingClient(options)`, since
            // there is no Context to supply; the point is that the code reads the same across
            // products.
            var client = new AgoraSignalingOptions
            {
                AppId = appId,
                UserId = userId,
                Token = string.IsNullOrEmpty(TokenEntry.Text) ? null : TokenEntry.Text.Trim(),
            }.CreateClient();

            // The SDK raises callbacks on its own thread; Append hops to the UI thread itself.
            client.MessageReceived += (_, ev) =>
                Append($"{ev.Publisher}: {ev.Text ?? $"[{ev.Data?.Length ?? 0} bytes]"}");
            client.ConnectionStateChanged += (_, ev) => Append($"connection: {ev.State} (reason {ev.Reason})");
            client.TokenPrivilegeWillExpire += (_, _) =>
                Append("token expires soon — fetch a fresh one and call RenewToken");

            // Both complete when the service confirms, so failures surface as exceptions here.
            await client.LoginAsync();
            await client.SubscribeAsync(channel);

            _client = client;
            _channel = channel;
            Append($"logged in as {userId}, subscribed to {channel}");
            SetLoggedIn(true);
        }
        catch (AgoraSignalingException exception)
        {
            Append($"failed: {exception.Message}");
            _client?.Dispose();
            _client = null;
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Unsubscribe first — logging out would drop the subscription anyway, but a real app
        // leaves rooms it is done with while staying logged in, so the call is worth showing.
        try
        {
            if (_client is not null && _channel is not null)
            {
                await _client.UnsubscribeAsync(_channel);
                Append($"unsubscribed from {_channel}");
            }
        }
        catch (AgoraSignalingException exception)
        {
            Append($"unsubscribe failed: {exception.Message}");
        }

        _client?.Logout();
        _client?.Dispose();
        _client = null;
        _channel = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (_client is null || _channel is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await _client.PublishAsync(_channel, message);

            // Subscribers receive their own channel's traffic, but a publisher does not get its
            // own message back — echo it locally so the room reads naturally.
            Append($"you: {message}");
            MessageEntry.Text = "";
        }
        catch (AgoraSignalingException exception)
        {
            Append($"send failed: {exception.Message}");
        }
    }

    private void SetLoggedIn(bool loggedIn)
    {
        LoginButton.IsEnabled = !loggedIn;
        LogoutButton.IsEnabled = loggedIn;
        MessageEntry.IsEnabled = loggedIn;
        SendButton.IsEnabled = loggedIn;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        MessagesLabel.Text = _log.ToString();
        MessagesScroll.ScrollToAsync(0, MessagesLabel.Height, animated: false);
    });

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
