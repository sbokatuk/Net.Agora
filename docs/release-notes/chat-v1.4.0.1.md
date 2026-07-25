## What's changed

**Chat debuts**: `Net.Agora.Chat` / `.Maui` **1.4.0.1** — one awaitable API over Agora Chat (IM
1.4.0) on Android and iOS. Sign in and out, send text messages, receive them through a
`MessageReceived` event, read the local conversation list, rotate the token, and be told when the
service ends the session and why.

```csharp
var chat = new AgoraChatOptions
{
    AppId = "your-app-id",
    UserId = "alice",
    Token = "chat-token-from-your-server",   // required — Chat has no App ID-only mode
}.CreateClient();

chat.MessageReceived += (_, e) => Show($"{e.Message.From}: {e.Message.Text}");

await chat.LoginAsync();
await chat.SendTextMessageAsync("bob", "hi");
```

Unlike Signaling, Chat gets a MAUI companion: Android's `ChatClient.Init` takes a `Context`, so
`Net.Agora.Chat.Maui`'s `CreateClient()` is what keeps an app free of `#if ANDROID`. The two SDKs
hide a real difference behind it — Android reports a send through a callback attached to the
message, iOS through a block on the send call — which the on-device suite exercises directly.

The device suite gained a Chat flavour (`-p:AgoraDeviceProduct=Chat`): construct, guard validation,
a full async round trip without credentials (sending before a sign-in is refused with `201` through
the awaitable path on both platforms), an empty local conversation list, cancellation vs. timeout,
sign-out/dispose hygiene. `samples/Net.Agora.Sample.Chat` is a one-to-one MAUI chat.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Chat` / `.Maui` | 1.4.0.1 | `Net.Agora.Chat.Android` [1.4.0.1], `Net.Agora.Chat.iOS` [1.4.0.1] |
