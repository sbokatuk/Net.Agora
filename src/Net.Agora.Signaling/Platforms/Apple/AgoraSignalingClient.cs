using Foundation;
using Net.Agora.Signaling.iOS;

namespace Net.Agora.Signaling;

public sealed partial class AgoraSignalingClient
{
    private readonly AgoraRtmClientKit _client;
    private readonly Delegate _delegate;

    /// <summary>
    /// Creates the client and logs nothing in yet — call <see cref="IAgoraSignalingClient.LoginAsync"/>.
    /// </summary>
    /// <param name="options">Must have <see cref="AgoraSignalingOptions.AppId"/> and <see cref="AgoraSignalingOptions.UserId"/> set.</param>
    public AgoraSignalingClient(AgoraSignalingOptions options)
    {
        options.Validate();
        _options = options;

        _delegate = new Delegate(this);
        var config = new AgoraRtmClientConfig(options.AppId!, options.UserId!);

        // initWithConfig:delegate:error: returns nil with the error populated for a config the
        // SDK rejects outright (options.Validate() catches the empty cases first).
        _client = new AgoraRtmClientKit(config, _delegate, out var error);
        if (_client.Handle == ObjCRuntime.NativeHandle.Zero)
        {
            throw new AgoraSignalingException(
                error?.LocalizedDescription ?? "AgoraRtmClientKit rejected the configuration.",
                errorCode: (int)(error?.Code ?? 0));
        }
    }

    private void LoginCore(string token, Action<Exception?> complete) =>
        _client.Login(token, Completion(complete));

    private void LogoutCore() => _client.Logout(null);

    private void SubscribeCore(string channelName, Action<Exception?> complete) =>
        _client.Subscribe(channelName, option: null, Completion(complete));

    private void UnsubscribeCore(string channelName, Action<Exception?> complete) =>
        _client.Unsubscribe(channelName, Completion(complete));

    private void PublishCore(string channelName, string message, Action<Exception?> complete) =>
        _client.Publish(channelName, message, option: null, Completion(complete));

    private void RenewTokenCore(string token) => _client.RenewToken(token, null);

    private void DisposeCore() => _client.Destroy();

    private static AgoraRtmOperationHandler Completion(Action<Exception?> complete) =>
        (response, errorInfo) =>
        {
            // Exactly one of the two parameters is nil — and "no error" is the success signal,
            // since some operations answer with an empty response object.
            if (errorInfo is null || errorInfo.ErrorCode == 0)
            {
                complete(null);
                return;
            }

            complete(new AgoraSignalingException(
                $"{errorInfo.Operation} failed: {errorInfo.Reason}", (int)errorInfo.ErrorCode));
        };

    /// <summary>
    /// Translates <c>AgoraRtmClientDelegate</c>'s (all-optional) callbacks into the shared
    /// partial's Raise* calls.
    /// </summary>
    private sealed class Delegate(AgoraSignalingClient owner) : AgoraRtmClientDelegate
    {
        public override void DidReceiveMessageEvent(AgoraRtmClientKit rtmKit, AgoraRtmMessageEvent @event)
        {
            // Exactly one of StringData/RawData is non-nil — see the binding.
            var message = @event.Message;
            owner.RaiseMessageReceived(
                @event.ChannelName,
                @event.Publisher,
                message?.StringData,
                message?.RawData?.ToArray());
        }

        public override void ConnectionChangedToState(
            AgoraRtmClientKit rtmKit, string channelName, AgoraRtmClientConnectionState state, nint reason) =>
            owner.RaiseConnectionStateChanged((AgoraConnectionState)(long)state, (int)reason);

        public override void TokenPrivilegeWillExpire(AgoraRtmClientKit rtmKit, string? channel) =>
            owner.RaiseTokenPrivilegeWillExpire();
    }
}
