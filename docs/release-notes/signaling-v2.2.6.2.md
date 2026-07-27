## What's changed

`Net.Agora.Signaling` / `.Maui` advance to **2.2.6.2**.

### A neutral target framework, so shared code can reference the package

The base package now also builds plain `net8.0`/`net9.0`/`net10.0`. A shared class library, a
ViewModel project or a test project can reference it and program against `IAgoraSignalingClient`;
constructing the client there throws `PlatformNotSupportedException` naming the platform heads.
Previously such a project failed to restore with an unexplained NU1202.

### Exceptions carry what they used to drop

`AgoraSignalingException` gained an overload that keeps the platform exception as
`InnerException`, so a native cause is no longer flattened into a message string.

### macOS

The macOS (AppKit) leg pins `Net.Agora.Signaling.Mac` **2.2.8.2** — a newer native RTM line than
the mobile packages' 2.2.6, because Agora publishes the Apple RTM SDK through a Swift package whose
macOS slices moved ahead. There is no MAUI companion for macOS: MAUI's only desktop-Mac target is
Mac Catalyst, which Agora ships no slice for.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Signaling` | 2.2.6.2 | `Net.Agora.Signaling.Android` [2.2.6.2], `Net.Agora.Signaling.iOS` [2.2.6.2], `Net.Agora.Signaling.Mac` [2.2.8.2] |
| `Net.Agora.Signaling.Maui` | 2.2.6.2 | `Net.Agora.Signaling` [2.2.6.2] |
