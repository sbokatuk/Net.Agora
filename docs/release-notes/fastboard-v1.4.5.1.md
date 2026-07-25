## What's changed

**Fastboard debuts**: `Net.Agora.Fastboard` / `.Maui` **1.4.5.1** — the same whiteboard with
netless's toolbar already drawn and wired, for an app that wants a working board rather than a
canvas to build a UI around. Same identifiers as `Net.Agora.Whiteboard`; swap
`AgoraWhiteboardView` for `AgoraFastboardView` and the toolbar comes up with the board.

Its MAUI package earns more than the whiteboard's: Android hands the SDK a view the app supplies
while iOS's Fastboard creates its own, and `CreateClient()` is where that difference disappears.
The device flavour, like the whiteboard's, refuses an unregistered room with the SDK's own message
on both platforms — and on iOS that proves a chain of Swift and Objective-C layers, linked out of
one static library, actually resolves at runtime. `samples/Net.Agora.Sample.Fastboard` is the same
board with its own toolbar.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Fastboard` / `.Maui` | 1.4.5.1 | `Net.Agora.Fastboard.Android` [1.8.1.1], `Net.Agora.Fastboard.iOS` [1.4.5.1] |
