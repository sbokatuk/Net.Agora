## What's changed

`Net.Agora.Fastboard` / `.Maui` advance to **1.4.5.2**.

### The MAUI sample now runs

`samples/Net.Agora.Sample.Fastboard` shipped without calling `.UseAgoraFastboard()` in its
`MauiProgram` — it carried a comment copy-pasted from the Chat sample claiming there was nothing to
register — so `Board.CreateClient(...)` threw at runtime in the reference sample. CI builds the
samples but never launches them, which is how it survived. Fixed, and a test now derives the rule
from the sample sources: any sample whose UI mentions an Agora view must register the matching
handler.

If you copied that sample, add the builder call:

```csharp
builder
    .UseMauiApp<App>()
    .UseAgoraFastboard();
```

### `AgoraFastboardException` gained `ErrorCode`, and keeps the cause

Every other product's exception had a code, so a single `catch` policy across products could not
read it. Expect 0 more often than not — netless reports board failures as text rather than a
numbered enumeration — with the message carrying the detail.

On Android the result callback used to flatten the Java exception into a message string; it now
travels as `InnerException`.

### A neutral target framework, so shared code can reference the package

The base package now also builds plain `net8.0`/`net9.0`/`net10.0`, so shared code can reference it
and program against `IAgoraFastboardClient`. Constructing the client there throws
`PlatformNotSupportedException` pointing at `AgoraFastboardView` on a platform head.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Fastboard` | 1.4.5.2 | `Net.Agora.Fastboard.Android` [1.8.1.2], `Net.Agora.Fastboard.iOS` [1.4.5.2] |
| `Net.Agora.Fastboard.Maui` | 1.4.5.2 | `Net.Agora.Fastboard` [1.4.5.2] |

The two platform packages sit on different native lines: netless versions the Android and iOS
Fastboard SDKs independently. No macOS leg.
