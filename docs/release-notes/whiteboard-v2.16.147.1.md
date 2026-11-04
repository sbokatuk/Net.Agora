## What's changed

`Net.Agora.Whiteboard` and `Net.Agora.Whiteboard.Maui` advance to **2.16.147.1**. The cross-platform
API is unchanged. Both platform pins move to newer netless releases, and this is the first release of
two Android fixes that were waiting on `main`.

| Platform | Before | Now |
| --- | --- | --- |
| Android | `Net.Agora.Whiteboard.Android` 2.16.123.2 | [2.16.134.1](https://github.com/sbokatuk/Net.Agora.Android/releases/tag/whiteboard-v2.16.134.1) |
| iOS | `Net.Agora.Whiteboard.iOS` 2.16.137.2 | [2.16.147.1](https://github.com/sbokatuk/Net.Agora.iOS/releases/tag/whiteboard-v2.16.147.1) |

### net8 MAUI apps on Android build again

On `net8.0-android34.0`, a MAUI app that referenced this package failed in D8 with
`Type androidx.collection.ArrayMapKt is defined multiple times`. The Android binding now asks for
lower AndroidX floors on net8, and such an app builds with no duplicate classes. net9 and net10 are
unchanged.

- **Remove any `-ktx` pins you added to get past that failure.** With the new floors,
  `Xamarin.AndroidX.Collection.Ktx` 1.5.0.3 and `Xamarin.AndroidX.Lifecycle.Runtime.Ktx` 2.9.2.1 now
  break the build instead: D8 fails on `androidx.collection.ArraySet$ElementIterator`. This
  repository's sample had those pins, and they're gone.
- **A plain .NET for Android app on net8 gets older AndroidX** (WebKit 1.4.0.8 and Core 1.8.0.1
  instead of 1.14.0.1 and 1.16.0.3). If you relied on this package to bring newer AndroidX,
  reference it yourself.

### Android: the board paints, and the documented regions work

- **Strokes paint.** On Android the board's web page left its canvas with a height of 0, so nothing
  drawn appeared and no stroke reached the room, with no error anywhere. The client now gives the
  page its real height after joining.
- **`Region` takes netless's keys.** `AgoraWhiteboardOptions.Region` documents keys such as `us-sv`,
  but the Android client passed them to a Java enum lookup that knows only constant names, so four of
  the five documented keys threw `Java.Lang.IllegalArgumentException`. They now map to the right
  region, and an unknown key throws `AgoraWhiteboardException`. A room can only be joined from the
  region it was created in.

### What netless changed

Android moves from 2.16.123 to 2.16.134 and iOS from 2.16.137 to 2.16.147. Among the changes: room
and player sessions are replaced one at a time and stale callbacks are ignored, per-region
configuration can come from netless's own storage, and the embedded web SDK moves to `white-web-sdk`
2.16.59. Nothing the façade calls changed. The binding notes linked above have the details.

### Known issue

Undo and Redo still do nothing on either platform, because the façade never turns serialization on
for the room. That is [#20](https://github.com/sbokatuk/Net.Agora/issues/20). The iOS binding now has
the `DisableSerialization` call the fix needs.

## Tested

- Package and unit tests, and the device suites for Whiteboard on `net8`, `net9` and `net10` on an
  Android emulator and an iOS simulator, plus `net9` with R8 shrinking on Android.
- The sample restores and builds on `net8.0-android34.0` under the .NET 8 SDK, without the removed
  pins and with no duplicate classes.
- Live, in a private room created for the run: the MAUI sample on Android and iOS, `net8` and
  `net9`, joined the room through its region field. Shapes drawn on Android appeared on iOS, and an
  iOS client that joined later received the room's earlier strokes.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Whiteboard` | 2.16.147.1 |
| `Net.Agora.Whiteboard.Maui` | 2.16.147.1 |

Platform pins: `Net.Agora.Whiteboard.Android` 2.16.134.1, `Net.Agora.Whiteboard.iOS` 2.16.147.1.
