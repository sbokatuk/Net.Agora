using Agora.Chat;
using Android.Content;
using IO.Agora;

using NativeChatClient = Agora.Chat.ChatClient;
using NativeChatMessage = Agora.Chat.ChatMessage;
using NativeChatOptions = Agora.Chat.ChatOptions;
using NativeConversation = Agora.Chat.Conversation;

namespace Net.Agora.Chat;

public sealed partial class AgoraChatClient
{
    private readonly NativeChatClient _client;
    private readonly ChatManager _chatManager;
    private readonly ConnectionListener _connectionListener;
    private readonly MessageListener _messageListener;

    /// <summary>
    /// Creates the client and signs nothing in yet — call <see cref="IAgoraChatClient.LoginAsync"/>.
    /// </summary>
    /// <param name="options">See <see cref="AgoraChatOptions.Validate"/> for what is required.</param>
    /// <param name="context">
    /// An Android <c>Context</c> — <c>ChatClient.Init</c> needs one. Use the MAUI companion
    /// package's <c>CreateClient()</c> to have this supplied for you.
    /// </param>
    public AgoraChatClient(AgoraChatOptions options, Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        options.Validate();
        _options = options;

        var nativeOptions = new NativeChatOptions { AutoLogin = options.AutoLogin };

        // Exactly one of the two is set — Validate() rejects both and neither.
        if (!string.IsNullOrWhiteSpace(options.AppId))
        {
            nativeOptions.AppId = options.AppId;
        }
        else
        {
            nativeOptions.AppKey = options.AppKey;
        }

        _client = NativeChatClient.Instance
            ?? throw new AgoraChatException("ChatClient.Instance returned null.", errorCode: 0);

        // Init is process-wide and idempotent in the SDK's own implementation: it returns early
        // once initialised. That matters because the singleton outlives any one AgoraChatClient,
        // so a second client in the same process reaches this line again.
        _client.Init(context.ApplicationContext ?? context, nativeOptions);

        _chatManager = _client.ChatManager()
            ?? throw new AgoraChatException("ChatClient.ChatManager() returned null.", errorCode: 0);

        _connectionListener = new ConnectionListener(this);
        _messageListener = new MessageListener(this);
        _client.AddConnectionListener(_connectionListener);
        _chatManager.AddMessageListener(_messageListener);
    }

    /// <inheritdoc />
    public string? CurrentUserId => string.IsNullOrEmpty(_client.CurrentUser) ? null : _client.CurrentUser;

    private void LoginCore(string userId, string token, Action<AgoraChatException?> complete) =>
        _client.LoginWithToken(userId, token, new Callback(complete));

    private void LogoutCore(bool unbindDeviceToken, Action<AgoraChatException?> complete) =>
        _client.Logout(unbindDeviceToken, new Callback(complete));

    private void RenewTokenCore(string token, Action<AgoraChatException?> complete) =>
        _client.RenewToken(token, new Callback(complete));

    private void SendTextMessageCore(
        string conversationId,
        string text,
        AgoraChatType chatType,
        Action<AgoraChatMessage, AgoraChatException?> complete)
    {
        var message = NativeChatMessage.CreateTextSendMessage(text, conversationId)
            ?? throw new AgoraChatException("ChatMessage.CreateTextSendMessage returned null.", errorCode: 0);

        message.SetChatType(ToNativeChatType(chatType));

        // The result does not come back from SendMessage — it is fire-and-forget, and the SDK
        // reports the outcome through a callback attached to the message. iOS takes the completion
        // block on the send call itself; this is the difference the façade exists to hide.
        message.SetMessageStatusCallback(new Callback(failure =>
            complete(failure is null ? ToMessage(message) : null!, failure)));

        _chatManager.SendMessage(message);
    }

    private IReadOnlyList<AgoraChatConversation> GetConversationsCore()
    {
        // BySort: most recent first, which is the order the façade documents. The unsorted
        // GetAllConversations returns a Map, whose iteration order is unspecified. (The generator
        // turns Java's getAllConversationsBySort() into a property, hence no call parentheses.)
        var conversations = _chatManager.AllConversationsBySort;
        if (conversations is null)
        {
            return [];
        }

        var result = new List<AgoraChatConversation>(conversations.Count);
        foreach (var conversation in conversations)
        {
            if (conversation is NativeConversation native)
            {
                result.Add(ToConversation(native));
            }
        }

        return result;
    }

    private void DisposeCore()
    {
        _client.RemoveConnectionListener(_connectionListener);
        _chatManager.RemoveMessageListener(_messageListener);

        // Deliberately no ChatClient teardown: Init is process-wide and the SDK offers no
        // matching de-init, so tearing the singleton down here would break any other client in
        // the same process. Signing out is what ends the session.
    }

    private static NativeChatMessage.ChatType ToNativeChatType(AgoraChatType chatType) => chatType switch
    {
        AgoraChatType.GroupChat => NativeChatMessage.ChatType.GroupChat!,
        AgoraChatType.ChatRoom => NativeChatMessage.ChatType.ChatRoom!,
        _ => NativeChatMessage.ChatType.Chat!,
    };

    private static AgoraChatType ToChatType(NativeChatMessage.ChatType? chatType)
    {
        if (chatType is null)
        {
            return AgoraChatType.Chat;
        }

        // Compared by identity against the SDK's own enum singletons: these are Java enums, bound
        // as classes rather than as C# enums, so there is no numeric value to switch on.
        if (chatType.Equals(NativeChatMessage.ChatType.GroupChat))
        {
            return AgoraChatType.GroupChat;
        }

        return chatType.Equals(NativeChatMessage.ChatType.ChatRoom)
            ? AgoraChatType.ChatRoom
            : AgoraChatType.Chat;
    }

    private static AgoraChatType ToChatType(NativeConversation.ConversationType? type)
    {
        if (type is null)
        {
            return AgoraChatType.Chat;
        }

        if (type.Equals(NativeConversation.ConversationType.GroupChat))
        {
            return AgoraChatType.GroupChat;
        }

        return type.Equals(NativeConversation.ConversationType.ChatRoom)
            ? AgoraChatType.ChatRoom
            : AgoraChatType.Chat;
    }

    private static AgoraChatMessage ToMessage(NativeChatMessage message) =>
        new(
            message.MsgId ?? "",
            message.ConversationId() ?? "",
            message.From ?? "",
            message.To ?? "",
            // Only a text body is surfaced; every other body type comes through with null text.
            (message.Body as TextMessageBody)?.Message,
            ToChatType(message.GetChatType()),
            DateTimeOffset.FromUnixTimeMilliseconds(message.MsgTime));

    private static AgoraChatConversation ToConversation(NativeConversation conversation) =>
        new(
            conversation.ConversationId() ?? "",
            ToChatType(conversation.Type),
            conversation.UnreadMsgCount,
            conversation.LastMessage is { } last ? ToMessage(last) : null);

    /// <summary>
    /// One native operation's completion. Every asynchronous Chat call on Android answers through
    /// this same <c>CallBack</c> interface, which is what makes the awaitable surface possible
    /// without a state machine.
    /// </summary>
    private sealed class Callback(Action<AgoraChatException?> complete) : Java.Lang.Object, ICallBack
    {
        public void OnSuccess() => complete(null);

        public void OnError(int code, string? description) =>
            complete(new AgoraChatException(description ?? "the operation failed.", code));
    }

    /// <summary>
    /// Translates <c>ConnectionListener</c>'s callbacks into the shared partial's Raise* calls.
    /// The On*Legacy members are the SDK's deprecated logout overloads, renamed in the binding —
    /// see the Metadata.xml in sbokatuk/Net.Agora.Android — and deliberately ignored here in
    /// favour of the current one.
    /// </summary>
    private sealed class ConnectionListener(AgoraChatClient owner) : Java.Lang.Object, IConnectionListener
    {
        public void OnConnected() =>
            owner.RaiseConnectionStateChanged(AgoraChatConnectionState.Connected);

        public void OnDisconnected(int errorCode) =>
            owner.RaiseConnectionStateChanged(AgoraChatConnectionState.Disconnected);

        public void OnLogout(int errorCode, LoginExtensionInfo? info) =>
            owner.RaiseForcedLogout(ToLogoutReason(errorCode));

        public void OnTokenWillExpire() => owner.RaiseTokenPrivilegeWillExpire();

        public void OnTokenExpired() => owner.RaiseForcedLogout(AgoraChatLogoutReason.TokenExpired);

        private static AgoraChatLogoutReason ToLogoutReason(int errorCode) => errorCode switch
        {
            Error.UserLoginAnotherDevice or Error.UserKickedByOtherDevice or Error.UserBindAnotherDevice
                => AgoraChatLogoutReason.LoggedInElsewhere,
            Error.UserRemoved => AgoraChatLogoutReason.AccountRemoved,
            Error.TokenExpired => AgoraChatLogoutReason.TokenExpired,
            _ => AgoraChatLogoutReason.Other,
        };
    }

    /// <summary>
    /// Translates <c>MessageListener</c>'s callbacks into the shared partial's Raise* calls. Only
    /// arrival is surfaced; read receipts, delivery receipts, recall, reactions and pinning are
    /// not part of this façade.
    /// </summary>
    private sealed class MessageListener(AgoraChatClient owner) : Java.Lang.Object, IMessageListener
    {
        public void OnMessageReceived(IList<NativeChatMessage>? messages)
        {
            if (messages is null)
            {
                return;
            }

            foreach (var message in messages)
            {
                owner.RaiseMessageReceived(ToMessage(message));
            }
        }
    }
}
