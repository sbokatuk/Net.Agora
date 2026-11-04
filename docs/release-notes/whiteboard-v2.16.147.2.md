## What's changed

`Net.Agora.Whiteboard` and `Net.Agora.Whiteboard.Maui` advance to **2.16.147.2**. The platform pins
don't move. This release makes Undo and Redo work, which fixes
[#20](https://github.com/sbokatuk/Net.Agora/issues/20).

### Undo and Redo work, on Android and iOS

Before this release, `HistoryChanged` was never raised and `Undo()` and `Redo()` did nothing, however
much was drawn. A netless room keeps no undo history until a client turns serialization on, and the
client never did. It now does this after a writable join, and again when `SetWritableAsync(true)` grants
writing.

- **`HistoryChanged` is raised as soon as a writable join completes**, with 0 undo and 0 redo steps.
  After that it's raised as the history changes. An app that enables its Undo and Redo buttons from
  this event, like the sample, now gets them enabled after a stroke.
- **A read-only join has no history.** The room refuses to turn serialization on for a client that
  can't write, so a client that joins with `Writable = false` isn't told anything until
  `SetWritableAsync(true)` completes.
- **Every client in the room must be on whiteboard SDK 2.9.3 or later (Web 2.9.2).** Upstream warns
  that older app clients crash once a client turns serialization on. Both platform pins are far past
  that. Check for older clients only if your room is shared with apps you don't control.

The API is unchanged.

### The sample's buttons fit on a phone

`Net.Agora.Sample.Whiteboard` put its seven buttons in one row, which is wider than a phone. On a
Pixel-sized screen, Redo was cut off and Clear couldn't be reached. The row now wraps.

## Tested

- The Whiteboard device suite has two new live checks. They're skipped without a room, which is how
  CI runs them. One draws a stroke and checks that it's one undo step, that Undo takes it off the
  scene and that Redo puts the same stroke back. The other joins read-only and checks that history
  arrives only once writing is granted. Both passed on an Android emulator (API 35, `net9`) and an
  iPhone 17 Pro simulator (iOS 26.5, `net9`), in a private room created for the run. Against the
  published 2.16.147.1, both fail on Android and iOS: `HistoryChanged` is never raised.
- The MAUI sample on Android and iOS, `net9`: after a stroke, Undo is enabled and Redo isn't. Undo
  takes the stroke off the board and enables Redo, and Redo puts it back. On Android the strokes and
  button presses were real touches, and all seven buttons are on screen.
- CI: package, unit and device-suite legs for every target framework, and the sample builds.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Whiteboard` | 2.16.147.2 |
| `Net.Agora.Whiteboard.Maui` | 2.16.147.2 |

Platform pins: `Net.Agora.Whiteboard.Android` 2.16.134.1, `Net.Agora.Whiteboard.iOS` 2.16.147.1.
