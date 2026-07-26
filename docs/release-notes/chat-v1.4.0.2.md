## What's changed

`Net.Agora.Chat` / `.Maui` advance to **1.4.0.2**.

### A neutral target framework, so shared code can reference the package

The base package now also builds plain `net8.0`/`net9.0`/`net10.0`. A shared class library, a
ViewModel project or a test project can reference it and program against `IAgoraChatClient`;
constructing the client there throws `PlatformNotSupportedException` naming the platform heads.
Previously such a project failed to restore with an unexplained NU1202.

### Exceptions carry what they used to drop

`AgoraChatException` gained an overload that keeps the platform exception as `InnerException`, so a
native cause is no longer flattened into a message string.

Worth repeating from the interface docs, since Chat is the one product where it differs: on Apple
the SDK delivers its callbacks on the main queue, so Chat events arrive there; on Android they
arrive on an SDK thread and must be marshalled before touching UI.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Chat` | 1.4.0.2 | `Net.Agora.Chat.Android` [1.4.0.2], `Net.Agora.Chat.iOS` [1.4.0.2] |
| `Net.Agora.Chat.Maui` | 1.4.0.2 | `Net.Agora.Chat` [1.4.0.2] |

No macOS leg: Agora ships no native macOS Chat SDK.
