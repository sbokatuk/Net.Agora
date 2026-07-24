using Agora.Rtm;

namespace Net.Agora.Signaling;

public sealed partial class AgoraSignalingClient
{
    private readonly RtmClient _client;
    private readonly Listener _listener;

    /// <summary>
    /// Creates the client and logs nothing in yet — call <see cref="IAgoraSignalingClient.LoginAsync"/>.
    /// Unlike the RTC engines, RTM needs no Android <c>Context</c>, so there is no MAUI companion
    /// package: this constructor is directly usable from any Android code.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraSignalingOptions.AppId"/> and <see cref="AgoraSignalingOptions.UserId"/> set.</param>
    public AgoraSignalingClient(AgoraSignalingOptions options)
    {
        options.Validate();
        _options = options;

        _listener = new Listener(this);

        var config = new RtmConfig.Builder(options.AppId, options.UserId)
            .EventListener(_listener)
            .Build();

        _client = RtmClient.Create(config)
            ?? throw new AgoraSignalingException("RtmClient.Create returned null.", errorCode: 0);
    }

    private void LoginCore(string token, Action<Exception?> complete) =>
        _client.Login(token, new Callback(complete));

    private void LogoutCore() => _client.Logout(null);

    private void SubscribeCore(string channelName, Action<Exception?> complete) =>
        _client.Subscribe(channelName, new SubscribeOptions(), new Callback(complete));

    private void UnsubscribeCore(string channelName, Action<Exception?> complete) =>
        _client.Unsubscribe(channelName, new Callback(complete));

    private void PublishCore(string channelName, string message, Action<Exception?> complete) =>
        _client.Publish(channelName, message, new PublishOptions(), new Callback(complete));

    private void RenewTokenCore(string token) => _client.RenewToken(token, null);

    private void DisposeCore()
    {
        RtmClient.Release();
    }

    private static AgoraSignalingException ToException(ErrorInfo? errorInfo, string fallback)
    {
        if (errorInfo is null)
        {
            return new AgoraSignalingException(fallback, errorCode: 0);
        }

        var code = errorInfo.ErrorCode is { } errorCode ? RtmConstants.RtmErrorCode.GetValue(errorCode) : 0;
        return new AgoraSignalingException(
            $"{errorInfo.Operation ?? "operation"} failed: {errorInfo.ErrorReason ?? fallback}", code);
    }

    /// <summary>
    /// One native operation's completion — RTM reports every call through its own
    /// <c>ResultCallback</c> rather than an event, which is what makes the awaitable surface
    /// possible without a state machine.
    /// </summary>
    private sealed class Callback(Action<Exception?> complete) : Java.Lang.Object, IResultCallback
    {
        public void OnSuccess(Java.Lang.Object? responseInfo) => complete(null);

        public void OnFailure(ErrorInfo? errorInfo) =>
            complete(ToException(errorInfo, "the operation failed."));
    }

    /// <summary>
    /// Translates <c>RtmEventListener</c>'s callbacks — a Java interface with default methods —
    /// into the shared partial's Raise* calls.
    /// </summary>
    private sealed class Listener(AgoraSignalingClient owner) : Java.Lang.Object, IRtmEventListener
    {
        public void OnMessageEvent(MessageEvent? e)
        {
            if (e?.ChannelName is not { } channelName)
            {
                return;
            }

            // The payload is either a Java String or a Java byte[] — RtmMessage.Data is typed
            // Object and RtmMessage.Type says which. Exactly one of text/raw comes out non-null.
            string? text = null;
            byte[]? raw = null;
            if (e.Message?.Data is { } data)
            {
                if (data is Java.Lang.String javaString)
                {
                    text = javaString.ToString();
                }
                else
                {
                    // global:: is required: the referenced binding assembly's root namespace is
                    // Net.Agora.Signaling.Android, so an unqualified "Android.Runtime" resolves
                    // against that sibling namespace first — same wart as Net.Agora.Video.Maui.
                    raw = global::Android.Runtime.JNIEnv.GetArray<byte>(data.Handle);
                }
            }

            owner.RaiseMessageReceived(channelName, e.PublisherId ?? "", text, raw);
        }

        public void OnConnectionStateChanged(
            string? channelName, RtmConstants.RtmConnectionState? state, RtmConstants.RtmConnectionChangeReason? reason)
        {
            if (state is null)
            {
                return;
            }

            owner.RaiseConnectionStateChanged(
                (AgoraConnectionState)RtmConstants.RtmConnectionState.GetValue(state),
                reason is null ? 0 : RtmConstants.RtmConnectionChangeReason.GetValue(reason));
        }

        public void OnTokenPrivilegeWillExpire(string? channelName) =>
            owner.RaiseTokenPrivilegeWillExpire();
    }
}
