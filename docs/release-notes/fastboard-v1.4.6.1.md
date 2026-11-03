## What's changed

`Net.Agora.Fastboard` and `Net.Agora.Fastboard.Maui` advance to **1.4.6.1**. The cross-platform API
is unchanged. Both platform pins move to Fastboard's newest releases, which tighten how a room may be
used, and the Android client changes to match.

| Platform | Before | Now |
| --- | --- | --- |
| Android | `Net.Agora.Fastboard.Android` 1.8.1.2 | [1.8.2.1](https://github.com/sbokatuk/Net.Agora.Android/releases/tag/fastboard-v1.8.2.1), with `Net.Agora.Whiteboard.Android` 2.16.134.1 |
| iOS | `Net.Agora.Fastboard.iOS` 1.4.5.2 | [1.4.6.1](https://github.com/sbokatuk/Net.Agora.iOS/releases/tag/fastboard-v1.4.6.1), embedding Whiteboard 2.16.145 |

### Room calls are safe from any thread on Android

Fastboard 1.8.2 throws `Java.Lang.IllegalStateException` ("FastRoom must be used on the main thread")
from `Join`, `Disconnect` and `Destroy` on any other thread, and a `JoinAsync`, `Disconnect` or
`Dispose` that runs after an `await` can easily be on a pool thread. The client now runs those calls
on the main thread: straight away from it, queued from anywhere else. A queued join that fails still
completes `JoinAsync` with the failure. iOS needed no change, as its binding already does the same.

### What changes on iOS

These come from Fastboard 1.4.6 and its binding; see the iOS note linked above for details.

- **A join is refused unless the board is idle**: while another join is in flight, once joined, or
  while a disconnect is finishing. `JoinAsync` fails with
  `Wait for the current room operation to finish before starting another one.`
- **Failure messages are sentences**, in the device's language: `Join room error` where 1.4.5.2 gave
  the bare key `joinRoomError`. Code that matched on the key needs changing.
- **`SetWritableAsync` before a join completes** with `The room is not ready.` instead of never
  completing.
- After `Error on setupSDK`, the board refuses every later join, so create a new client to retry.

### net8 MAUI apps on Android build again

On `net8.0-android34.0`, a MAUI app with Fastboard failed in D8 with duplicate AndroidX classes. The
Android bindings now ask for lower AndroidX floors on net8, and such an app builds with no duplicate
classes. net9 and net10 are unchanged.

- **Remove any `-ktx` pins you added to get past that failure.** With the new floors they either
  break D8 on `androidx.collection.ArraySet$ElementIterator`, or raise Core and add `NU1608` warnings.
  This repository's sample had five pins, `Xamarin.AndroidX.Lifecycle.Common` among them, and now
  restores and builds without any of them, under the .NET 8 SDK too.
- **A plain .NET for Android app on net8 gets older AndroidX** (AppCompat 1.3.1.5 and RecyclerView
  1.2.1.9 instead of 1.7.0.7 and 1.3.2.10). If you relied on this package to bring newer AndroidX,
  reference it yourself.

On Android, `Fastboard.Version` still returns `1.8.1`: that is netless's constant, not a wrong payload.

## Tested

- Package and unit tests, and the device suites for Fastboard on `net8`, `net9` and `net10` on an
  Android emulator and an iOS simulator.
- The sample restores and builds on `net8.0-android34.0` under the .NET 8 SDK, without the removed
  pins and with no duplicate classes.
- Live, in a private room created for the run: the MAUI sample on Android and iOS, `net8` and
  `net9`, joined with the toolbar up. Strokes drawn on Android appeared on iOS, both switched to
  read-only and back, and both left and rejoined on the same view.
- `Disconnect` called from a pool thread on Android disconnected the room. Without the main-thread
  change, the same call threw `IllegalStateException`.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Fastboard` | 1.4.6.1 |
| `Net.Agora.Fastboard.Maui` | 1.4.6.1 |

Platform pins: `Net.Agora.Fastboard.Android` 1.8.2.1, `Net.Agora.Fastboard.iOS` 1.4.6.1.
