## What's changed

`Net.Agora.Signaling` and `Net.Agora.Signaling.Maui` advance to **2.2.6.3**. The cross-platform API
is unchanged; what moves is the platform packages underneath, and this is not a cosmetic bump.

### Using Signaling with Video or Voice needs this version

All three platform bindings shipped the same defect, found from the Android side and confirmed on a
device: `RtcEngine.Create()` returning `null` in an app that referenced Video and Signaling — no
exception, no logged reason, and a build that had passed cleanly.

Signaling and the RTC products each carry `aosl`, Agora's own infrastructure library, and every
copy lands at the *same path* in a consuming app — `lib/<abi>/libaosl.so` on Android,
`Frameworks/aosl.framework` on Apple. The app keeps one copy, whichever the build happened to keep,
so both products end up running against a library only one of them was built against. Until now
those copies were not interchangeable:

| Platform | RTC ships | Signaling shipped | Missing from Signaling's |
| --- | --- | --- | --- |
| Android | `aosl` 1.3.5 | agora-rtm's vendored `libaosl.so` | `aosl_ref_magic` |
| iOS | `aosl` 1.3.5 | `aosl` 1.2.13 | `aosl_ref_magic` |
| macOS | `aosl` 1.3.5 | `aosl` 1.3.0 | `_aosl_data_ptr` |

Each binding repository now takes `aosl` from the same source the RTC line uses, and each has its
own release note with the mechanics:
[Android `signaling-v2.2.6.3`](https://github.com/sbokatuk/Net.Agora.Android/blob/main/docs/release-notes/signaling-v2.2.6.3.md),
[iOS `signaling-v2.2.6.3`](https://github.com/sbokatuk/Net.Agora.iOS/blob/main/docs/release-notes/signaling-v2.2.6.3.md),
[macOS `signaling-v2.2.8.3`](https://github.com/sbokatuk/Net.Agora.Mac/blob/main/docs/release-notes/signaling-v2.2.8.3.md).

Nothing regressed for an app that uses Signaling on its own: on every platform, every `aosl` symbol
the RTM framework imports is exported by the version it now ships against.

### Why this side never caught it

The device test app has always held exactly one product, and no sample here referenced Signaling
alongside an RTC package — so the one configuration that fails was the one nothing built. The
emulator matrix now has a leg that references **Video and Signaling together** and runs the ordinary
Video suite on it, which is precisely the check that fails on the old packages and passes on these.
Both binding repositories gained the equivalent leg over their raw packages.

Nothing in a build could have caught it either way. The products are different Java packages and
different frameworks, so the app compiled, packaged and installed identically whether the merge kept
a working `aosl` or a broken one.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Signaling` | 2.2.6.3 |
| `Net.Agora.Signaling.Maui` | 2.2.6.3 |

Platform pins: `Net.Agora.Signaling.Android` 2.2.6.3, `Net.Agora.Signaling.iOS` 2.2.6.3,
`Net.Agora.Signaling.Mac` 2.2.8.3.
