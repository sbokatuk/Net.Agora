## What's changed

**`Net.Agora.Signaling.Maui` debuts** at **2.2.6.1**, alongside the unchanged `Net.Agora.Signaling`.

It exists for symmetry, not necessity. Signaling is the one product here that needs no platform
glue — RTM takes no Android `Context` and renders nothing — so `new AgoraSignalingClient(options)`
already works verbatim in a MAUI app. What the companion adds is a single `CreateClient()`
extension shaped exactly like the ones on `Net.Agora.Video.Maui` / `Voice.Maui` / `Chat.Maui`, so
a MAUI app constructs every Agora client the same way:

```csharp
var signaling = new AgoraSignalingOptions { AppId = "your-app-id", UserId = "alice" }.CreateClient();
```

An app that does not want the MAUI dependency keeps referencing `Net.Agora.Signaling` and calls the
constructor — nothing here is required. `samples/Net.Agora.Sample.Signaling` now goes through
`CreateClient()` to match the other samples.

`Net.Agora.Signaling` itself is unchanged at 2.2.6.1; the push republishes nothing (`--skip-duplicate`).

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Signaling` | 2.2.6.1 (unchanged) | `Net.Agora.Signaling.Android` [2.2.6.1], `Net.Agora.Signaling.iOS` [2.2.6.1] |
| `Net.Agora.Signaling.Maui` | 2.2.6.1 | `Net.Agora.Signaling` [2.2.6.1] |
