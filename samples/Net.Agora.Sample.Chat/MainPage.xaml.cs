using System.Text;
using Net.Agora.Chat;
using Net.Agora.Chat.Maui;

namespace Net.Agora.Sample.Chat;

/// <summary>
/// A one-to-one chat over Agora Chat: sign in, send text messages to another user, show what
/// arrives, and list the conversations the SDK has stored locally. The whole app is this one file
/// — there is no per-platform code, because <c>Net.Agora.Chat</c> presents the same client on
/// Android and iOS and <c>Net.Agora.Chat.Maui</c>'s <c>CreateClient()</c> hides the one thing that
/// does differ: Android's <c>ChatClient.Init</c> needs a <c>Context</c>.
///
/// Unlike the Signaling sample, a token is not optional here — Chat has no App ID-only mode, so
/// this needs a token from your own token server (or one generated in the Agora Console).
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private AgoraChatClient? _client;
    private string? _peer;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var userId = UserIdEntry.Text?.Trim();
        var peer = PeerEntry.Text?.Trim();
        var token = TokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(userId) ||
            string.IsNullOrEmpty(peer) || string.IsNullOrEmpty(token))
        {
            Append("enter an App ID, your user id, a peer and a token first");
            return;
        }

        LoginButton.IsEnabled = false;

        try
        {
            // CreateClient() is the MAUI companion package's one job — see
            // src/Net.Agora.Chat.Maui.
            var client = new AgoraChatOptions
            {
                AppId = appId,
                UserId = userId,
                Token = token,
            }.CreateClient();

            // The SDK raises callbacks on its own thread; Append hops to the UI thread itself.
            client.MessageReceived += (_, ev) =>
                Append($"{ev.Message.From}: {ev.Message.Text ?? "[non-text message]"}");
            client.ConnectionStateChanged += (_, ev) => Append($"connection: {ev.State}");
            client.ForcedLogout += (_, ev) =>
            {
                Append($"signed out by the service: {ev.Reason}");
                MainThread.BeginInvokeOnMainThread(() => SetSignedIn(false));
            };
            client.TokenPrivilegeWillExpire += (_, _) =>
                Append("token expires soon — fetch a fresh one and call RenewTokenAsync");

            // Completes when the service confirms, so failures surface as exceptions here.
            await client.LoginAsync();

            _client = client;
            _peer = peer;
            Append($"signed in as {client.CurrentUserId}, talking to {peer}");
            SetSignedIn(true);
        }
        catch (AgoraChatException exception)
        {
            Append($"failed: [{exception.ErrorCode}] {exception.Message}");
            _client?.Dispose();
            _client = null;
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            if (_client is not null)
            {
                await _client.LogoutAsync();
            }
        }
        catch (AgoraChatException exception)
        {
            Append($"sign-out failed: [{exception.ErrorCode}] {exception.Message}");
        }

        _client?.Dispose();
        _client = null;
        _peer = null;
        Append("signed out");
        SetSignedIn(false);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text;
        if (_client is null || _peer is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            // The returned message carries the server-assigned id and timestamp; the sender does
            // not get its own message back through MessageReceived, so echo it locally.
            var sent = await _client.SendTextMessageAsync(_peer, text);

            Append($"you: {sent.Text}  ({sent.MessageId})");
            MessageEntry.Text = "";
        }
        catch (AgoraChatException exception)
        {
            Append($"send failed: [{exception.ErrorCode}] {exception.Message}");
        }
    }

    private void OnConversationsClicked(object sender, EventArgs e)
    {
        if (_client is null)
        {
            return;
        }

        // Local only — this reads the SDK's own database rather than the server, so it is empty
        // on a fresh install until messages have been sent or received.
        var conversations = _client.GetConversations();
        if (conversations.Count == 0)
        {
            Append("no conversations stored locally yet");
            return;
        }

        foreach (var conversation in conversations)
        {
            Append($"  {conversation.ConversationId} ({conversation.Type}), " +
                $"{conversation.UnreadCount} unread, last: {conversation.LatestMessage?.Text ?? "—"}");
        }
    }

    private void SetSignedIn(bool signedIn)
    {
        LoginButton.IsEnabled = !signedIn;
        LogoutButton.IsEnabled = signedIn;
        ConversationsButton.IsEnabled = signedIn;
        MessageEntry.IsEnabled = signedIn;
        SendButton.IsEnabled = signedIn;
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
