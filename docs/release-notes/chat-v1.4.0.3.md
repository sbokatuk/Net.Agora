## What's changed

`Net.Agora.Chat` and `Net.Agora.Chat.Maui` advance to **1.4.0.3**. The cross-platform API is
unchanged; the iOS platform pin moves.

### `Net.Agora.Chat.iOS` 1.4.0.2 → 1.4.0.3

Chat is the one product whose `aosl` does not come out of its own archive — `Agora_Chat_iOS`
declares `AgoraInfra_iOS` as a pod dependency — and that pod moved to 1.3.9 while the Chat pod
itself stayed at 1.4.0. The iOS binding released that as 1.4.0.3 and this repository was still
pinning 1.4.0.2, so a consumer of the façade did not get it. See the
[iOS `chat-v1.4.0.3` note](https://github.com/sbokatuk/Net.Agora.iOS/blob/main/docs/release-notes/chat-v1.4.0.3.md).

`Net.Agora.Chat.Android` stays at 1.4.0.2. The two platform pins reading different fourth
components is not a mistake here: `AgoraInfra_iOS` has no Android counterpart, and the Chat SDK
itself is 1.4.0 on both.

Nothing else changes. `aosl` 1.3.9 is not identical to the 1.3.5 the RTC packages carry — it adds
three symbols and drops two — but it satisfies every SDK framework in play, so Chat alongside Video
or Signaling is unaffected either way. That is asserted rather than assumed: see the iOS
repository's `AoslCompatibilityTests`, added in the same round as
[`signaling-v2.2.6.3`](signaling-v2.2.6.3.md).

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Chat` | 1.4.0.3 |
| `Net.Agora.Chat.Maui` | 1.4.0.3 |

Platform pins: `Net.Agora.Chat.Android` 1.4.0.2, `Net.Agora.Chat.iOS` 1.4.0.3.
