## What's changed

`Net.Agora.Chat` and `Net.Agora.Chat.Maui` advance to **1.4.0.4**. The cross-platform API is
unchanged; the iOS platform pin moves.

### `Net.Agora.Chat.iOS` 1.4.0.3 → 1.4.0.4

The Chat pod is still 1.4.0. What moves is `AgoraInfra_iOS`, which supplies the `aosl` the package
carries: 1.3.9 becomes **1.3.16**, byte for byte the `aosl` inside the RTC 4.7.0 pods. It adds symbols
and removes none, so Chat keeps working beside Video, Voice or Signaling on either RTC line. See the
[iOS `chat-v1.4.0.4` note](https://github.com/sbokatuk/Net.Agora.iOS/releases/tag/chat-v1.4.0.4).

`Net.Agora.Chat.Android` stays at 1.4.0.2: `AgoraInfra_iOS` has no Android counterpart.

## Tested

- Package and unit tests, and the device suites for Chat on `net8`, `net9` and `net10` on an
  Android emulator and an iOS simulator.
- Live, with user tokens signed for the run: the MAUI sample signed in as one user on Android and as
  another on iOS, on `net8` and on `net9`, and messages went both ways. The conversation list showed
  the incoming conversation with its unread count.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Chat` | 1.4.0.4 |
| `Net.Agora.Chat.Maui` | 1.4.0.4 |

Platform pins: `Net.Agora.Chat.Android` 1.4.0.2, `Net.Agora.Chat.iOS` 1.4.0.4.
