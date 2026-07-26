## What's changed

`Net.Agora.Whiteboard` / `.Maui` advance to **2.16.137.2**.

### The MAUI sample now runs

`samples/Net.Agora.Sample.Whiteboard` shipped without calling `.UseAgoraWhiteboard()` in its
`MauiProgram` — it carried a comment copy-pasted from the Chat sample claiming there was nothing to
register — so `Board.CreateClient(...)` threw at runtime in the reference sample. CI builds the
samples but never launches them, which is how it survived. Fixed, and a test now derives the rule
from the sample sources: any sample whose UI mentions an Agora view must register the matching
handler.

If you copied that sample, add the builder call:

```csharp
builder
    .UseMauiApp<App>()
    .UseAgoraWhiteboard();
```

### `AgoraWhiteboardException` gained `ErrorCode`

Every other product's exception had one, so a single `catch` policy across products could not read
it. Expect 0 more often than not, and the message to carry the detail: netless reports whiteboard
failures as text — an `NSError` description on iOS, an exception message on Android — rather than as
a numbered enumeration like Agora's own SDKs. The type also gained an overload that keeps the
platform exception as `InnerException`.

### A neutral target framework, so shared code can reference the package

The base package now also builds plain `net8.0`/`net9.0`/`net10.0`, so shared code can reference it
and program against `IAgoraWhiteboardClient`. Constructing the client there throws
`PlatformNotSupportedException` pointing at `AgoraWhiteboardView.CreateClient` on a platform head.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Whiteboard` | 2.16.137.2 | `Net.Agora.Whiteboard.Android` [2.16.123.2], `Net.Agora.Whiteboard.iOS` [2.16.137.2] |
| `Net.Agora.Whiteboard.Maui` | 2.16.137.2 | `Net.Agora.Whiteboard` [2.16.137.2] |

The two platform packages sit on different native lines on purpose: netless keeps the Android and
iOS whiteboard SDKs in separate repositories with separate release cadences. No macOS leg — netless
publishes no native AppKit whiteboard.
