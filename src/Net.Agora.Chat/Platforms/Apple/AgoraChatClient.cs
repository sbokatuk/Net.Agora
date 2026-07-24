using Net.Agora.Chat.iOS;

using NativeChatClient = Net.Agora.Chat.iOS.AgoraChatClient;
using NativeChatMessage = Net.Agora.Chat.iOS.AgoraChatMessage;
using NativeChatOptions = Net.Agora.Chat.iOS.AgoraChatOptions;
using NativeChatType = Net.Agora.Chat.iOS.AgoraChatType;
using NativeConversation = Net.Agora.Chat.iOS.AgoraChatConversation;
// Aliased because the façade declares its own enums of the same names: this file's namespace is
// Net.Agora.Chat, so an unqualified AgoraChatConnectionState binds to the façade's, and an
// override of a binding method would not match.
using NativeConnectionState = Net.Agora.Chat.iOS.AgoraChatConnectionState;
using NativeConversationType = Net.Agora.Chat.iOS.AgoraChatConversationType;

namespace Net.Agora.Chat;

public sealed partial class AgoraChatClient
{
    private readonly NativeChatClient _client;
    private readonly ClientDelegate _clientDelegate;
    private readonly ManagerDelegate _managerDelegate;

    /// <summary>
    /// Creates the client and signs nothing in yet — call <see cref="IAgoraChatClient.LoginAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraChatOptions.Validate"/> for what is required.</param>
    public AgoraChatClient(AgoraChatOptions options)
    {
        options.Validate();
        _options = options;

        // Exactly one of the two is set — Validate() rejects both and neither.
        var nativeOptions = string.IsNullOrWhiteSpace(options.AppId)
            ? NativeChatOptions.FromAppKey(options.AppKey!)
            : NativeChatOptions.FromAppId(options.AppId!);

        nativeOptions.IsAutoLogin = options.AutoLogin;
        nativeOptions.EnableConsoleLog = options.EnableConsoleLog;

        _client = NativeChatClient.SharedClient;

        // Synchronous, and answers nil on success — the SDK's convention for its non-block calls.
        // Process-wide, like Android's Init: the singleton outlives any one AgoraChatClient.
        if (_client.InitializeSdk(nativeOptions) is { } error)
        {
            throw ToException(error, "initialising the Chat SDK failed.");
        }

        _clientDelegate = new ClientDelegate(this);
        _managerDelegate = new ManagerDelegate(this);

        // null queue means "call me on the main thread", which is what a UI-driven app wants and
        // what the Android listeners do by default.
        _client.AddDelegate(_clientDelegate, null);
        _client.ChatManager?.AddDelegate(_managerDelegate, null);
    }

    /// <inheritdoc />
    public string? CurrentUserId => _client.CurrentUsername;

    private void LoginCore(string userId, string token, Action<AgoraChatException?> complete) =>
        _client.Login(userId, token, (_, error) => complete(ToFailure(error, "sign-in failed.")));

    private void LogoutCore(bool unbindDeviceToken, Action<AgoraChatException?> complete) =>
        _client.Logout(unbindDeviceToken, error => complete(ToFailure(error, "sign-out failed.")));

    private void RenewTokenCore(string token, Action<AgoraChatException?> complete) =>
        _client.RenewToken(token, error => complete(ToFailure(error, "renewing the token failed.")));

    private void SendTextMessageCore(
        string conversationId,
        string text,
        AgoraChatType chatType,
        Action<AgoraChatMessage, AgoraChatException?> complete)
    {
        var message = new NativeChatMessage(conversationId, new AgoraChatTextMessageBody(text), null)
        {
            ChatType = ToNativeChatType(chatType),
        };

        // progress is for attachment uploads only — a text message never reports any.
        _client.ChatManager!.SendMessage(message, null, (sent, error) =>
        {
            var failure = ToFailure(error, "sending the message failed.");
            complete(failure is null ? ToMessage(sent ?? message) : null!, failure);
        });
    }

    private IReadOnlyList<AgoraChatConversation> GetConversationsCore()
    {
        var conversations = _client.ChatManager?.GetAllConversations();
        if (conversations is null)
        {
            return [];
        }

        var result = new List<AgoraChatConversation>(conversations.Length);
        foreach (var conversation in conversations)
        {
            result.Add(ToConversation(conversation));
        }

        // The SDK returns these unordered, where Android's GetAllConversationsBySort does not.
        // Sorted here so both platforms honour the façade's "most recent first".
        result.Sort(static (left, right) =>
            (right.LatestMessage?.Timestamp ?? DateTimeOffset.MinValue)
            .CompareTo(left.LatestMessage?.Timestamp ?? DateTimeOffset.MinValue));

        return result;
    }

    private void DisposeCore()
    {
        _client.RemoveDelegate(_clientDelegate);
        _client.ChatManager?.RemoveDelegate(_managerDelegate);

        // Deliberately no SDK teardown: InitializeSdk is process-wide and has no counterpart, so
        // there is nothing to undo here. Signing out is what ends the session.
    }

    private static NativeChatType ToNativeChatType(AgoraChatType chatType) => chatType switch
    {
        AgoraChatType.GroupChat => NativeChatType.GroupChat,
        AgoraChatType.ChatRoom => NativeChatType.ChatRoom,
        _ => NativeChatType.Chat,
    };

    private static AgoraChatType ToChatType(NativeChatType chatType) => chatType switch
    {
        NativeChatType.GroupChat => AgoraChatType.GroupChat,
        NativeChatType.ChatRoom => AgoraChatType.ChatRoom,
        _ => AgoraChatType.Chat,
    };

    private static AgoraChatType ToChatType(NativeConversationType type) => type switch
    {
        NativeConversationType.GroupChat => AgoraChatType.GroupChat,
        NativeConversationType.ChatRoom => AgoraChatType.ChatRoom,
        _ => AgoraChatType.Chat,
    };

    private static AgoraChatMessage ToMessage(NativeChatMessage message) =>
        new(
            message.MessageId ?? "",
            message.ConversationId ?? "",
            message.From ?? "",
            message.To ?? "",
            // Only a text body is surfaced; every other body type comes through with null text.
            (message.Body as AgoraChatTextMessageBody)?.Text,
            ToChatType(message.ChatType),
            DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp));

    private static AgoraChatConversation ToConversation(NativeConversation conversation) =>
        new(
            conversation.ConversationId ?? "",
            ToChatType(conversation.Type),
            conversation.UnreadMessagesCount,
            conversation.LatestMessage is { } latest ? ToMessage(latest) : null);

    private static AgoraChatException? ToFailure(AgoraChatError? error, string fallback) =>
        error is null ? null : ToException(error, fallback);

    private static AgoraChatException ToException(AgoraChatError error, string fallback) =>
        new(string.IsNullOrEmpty(error.ErrorDescription) ? fallback : error.ErrorDescription, (int)error.Code);

    /// <summary>
    /// Translates <c>AgoraChatClientDelegate</c>'s (all-optional) callbacks into the shared
    /// partial's Raise* calls.
    /// </summary>
    private sealed class ClientDelegate(AgoraChatClient owner) : AgoraChatClientDelegate
    {
        public override void ConnectionStateDidChange(NativeConnectionState state) =>
            owner.RaiseConnectionStateChanged(
                state == NativeConnectionState.Connected
                    ? AgoraChatConnectionState.Connected
                    : AgoraChatConnectionState.Disconnected);

        public override void UserAccountDidRemoveFromServer() =>
            owner.RaiseForcedLogout(AgoraChatLogoutReason.AccountRemoved);

        // Reached when another device signs in as the same user, and for the rarer server-side
        // causes; the error carries which, but the two enumerations line up with Android's so the
        // mapping stays here rather than in the shared half.
        public override void UserAccountDidForcedToLogout(AgoraChatError? error) =>
            owner.RaiseForcedLogout(ToLogoutReason((int)(error?.Code ?? 0)));

        public override void TokenWillExpire(nint errorCode) => owner.RaiseTokenPrivilegeWillExpire();

        public override void TokenDidExpire(nint errorCode) =>
            owner.RaiseForcedLogout(AgoraChatLogoutReason.TokenExpired);

        // The values are AgoraChatErrorCode's, which share their numbering with Android's Error
        // constants — 206/213/217 for a competing sign-in, 207 for a deleted account, 108 for an
        // expired token.
        private static AgoraChatLogoutReason ToLogoutReason(int errorCode) => errorCode switch
        {
            206 or 213 or 217 => AgoraChatLogoutReason.LoggedInElsewhere,
            207 => AgoraChatLogoutReason.AccountRemoved,
            108 => AgoraChatLogoutReason.TokenExpired,
            _ => AgoraChatLogoutReason.Other,
        };
    }

    /// <summary>
    /// Translates <c>AgoraChatManagerDelegate</c>'s (all-optional) callbacks into the shared
    /// partial's Raise* calls. Only arrival is surfaced; the conversation-list and status
    /// callbacks are not part of this façade.
    /// </summary>
    private sealed class ManagerDelegate(AgoraChatClient owner) : AgoraChatManagerDelegate
    {
        public override void MessagesDidReceive(NativeChatMessage[] messages)
        {
            foreach (var message in messages)
            {
                owner.RaiseMessageReceived(ToMessage(message));
            }
        }
    }
}
