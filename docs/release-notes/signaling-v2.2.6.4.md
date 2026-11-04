## What's changed

`Net.Agora.Signaling` and `Net.Agora.Signaling.Maui` advance to **2.2.6.4**. The cross-platform API
is unchanged. Two platform pins move, and on macOS the native SDK under the client moves from RTM
2.2.8 to 2.3.0.

### Use this version with Video or Voice 4.7.0

Signaling and the RTC products each ship an `aosl`, and an app keeps one copy at
`Frameworks/aosl.framework`, so both products run against whichever copy the build kept. RTC 4.7.0
moves that library to **1.3.16**, and its `AgoraRtcKit` imports five symbols that the 1.3.5 in
earlier Signaling packages never exported. An app on `Net.Agora.Video` or `Net.Agora.Voice` 4.7.0.1
with an older Signaling package could stop the RTC engine loading.

Both Apple platform packages now carry 1.3.16, byte for byte the copy the RTC 4.7.0 archives bundle:

| Platform | Before | Now |
| --- | --- | --- |
| iOS | `Net.Agora.Signaling.iOS` 2.2.6.3, `aosl` 1.3.5 | [2.2.6.4](https://github.com/sbokatuk/Net.Agora.iOS/releases/tag/signaling-v2.2.6.4), `aosl` 1.3.16 |
| macOS | `Net.Agora.Signaling.Mac` 2.2.8.3, RTM 2.2.8 | [2.3.0.2](https://github.com/sbokatuk/Net.Agora.Mac/releases/tag/signaling-v2.3.0.2), RTM 2.3.0, `aosl` 1.3.16 |
| Android | `Net.Agora.Signaling.Android` 2.2.6.3 | unchanged: Android's RTC 4.6.4 still uses `aosl` 1.3.5 |

It also works with the current RTC 4.6.2 packages, so it is safe to take before moving Video or Voice:
1.3.16 exports every `aosl` symbol RTC 4.6.2 imports, which the iOS binding's tests assert and which
was checked on both macOS slices for this release (144 symbols, none missing). Signaling on its own
was never affected.

### macOS: RTM 2.3.0 reports the link differently

RTM 2.3.0 deleted the connection-state callback in favour of a link-state event (see the
[`signaling-v2.3.0.1` note](https://github.com/sbokatuk/Net.Agora.Mac/releases/tag/signaling-v2.3.0.1)).
The client now listens to the new event on macOS and raises the same `ConnectionStateChanged`, mapped
like this:

| RTM 2.3.0 link state | `AgoraConnectionState` |
| --- | --- |
| `Connecting` | `Connecting` |
| `Connected` | `Connected` |
| `Suspended` | `Reconnecting` |
| `Failed` | `Failed` |
| `Idle`, `Disconnected` | `Disconnected` |

On macOS, `AgoraConnectionStateEventArgs.Reason` is now RTM 2.3.0's link-state change reason, which
uses different numbers from the connection-change reasons Android and iOS report. Compare states, not
reason codes, if your code runs on more than one platform. iOS and Android are unchanged.

## Tested

- Package and unit tests, and the device suites for Signaling on `net8`, `net9` and `net10` on an
  Android emulator, an iOS simulator and the macOS host.
- Live, with tokens signed for the run: the MAUI sample on Android and iOS, `net8` and `net9`, and
  `Net.Agora.Sample.Signaling.Mac` logged in to one channel and each received the other two's
  messages. On macOS the client reported `Connecting` then `Connected` through the ported event.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Signaling` | 2.2.6.4 |
| `Net.Agora.Signaling.Maui` | 2.2.6.4 |

Platform pins: `Net.Agora.Signaling.Android` 2.2.6.3, `Net.Agora.Signaling.iOS` 2.2.6.4,
`Net.Agora.Signaling.Mac` 2.3.0.2.
