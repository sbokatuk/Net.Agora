# Building Net.Agora

Everything here is driven by the pins in [`Directory.Build.props`](../Directory.Build.props) and
run by the scripts in `build/`. CI runs exactly these scripts, so a green local run means the same
thing a green pipeline does.

## Status and architecture

Three repositories, each independently versioned and released — see the root
[README](../README.md#how-this-repository-works):

- **[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android)** — the raw Android
  bindings (Video and Voice).
- **[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS)** — the raw iOS bindings (Video
  and Voice).
- **`Net.Agora`** (this repository) — the façades (`Net.Agora.Video`, `Net.Agora.Video.Maui`,
  `Net.Agora.Voice`, `Net.Agora.Voice.Maui`, `Net.Agora.Signaling`) only. It binds no native
  code itself.

**This repository binds nothing.** No `ApiDefinition.cs`, no `.aar`, no
`AndroidLibrary`/`NativeReference` — each façade depends on its platform packages the same way any
consumer would, at an exact pinned version. For how *those* bindings work — the
`AndroidMavenLibrary` mechanism, the hand-written iOS `ApiDefinition.cs` and why, the native SDK
pins — see each repository's own README and `docs/`.

## Layout

```
Directory.Build.props        NetAgora<Product>*Version pins (platform packages), TFM bands, shared metadata
global.json                  pins the .NET 9 SDK (the "net9 band")
NuGet.config                  nuget.org + ./artifacts, so tests and the sample consume packed packages
build/
  pins.sh                     the only parser of Directory.Build.props for shell callers
  BuildNugets.sh               packs Net.Agora.<Product> + .Maui -> ./artifacts (façade packages only)
  merge-packages.py            combines the two SDK-band passes into one package per id
src/
  Net.Agora.Video/              the cross-platform Video client
  Net.Agora.Video.Maui/         the MAUI video view + platform glue
  Net.Agora.Voice/              the cross-platform Voice client
  Net.Agora.Voice.Maui/         the MAUI platform glue (no view — voice renders nothing)
  Net.Agora.Signaling/          the cross-platform Signaling (RTM) client — no MAUI companion,
                                 nothing platform-specific to hide
tests/
  Net.Agora.PackageTests/       asserts this repository's own packages' shape and pinned dependencies
  Net.Agora.UnitTests/          the platform-neutral façade logic — options validation, the
                                 event-arg types, for both products — no device, no packages, no workload
  Net.Agora.DeviceTests/        on-device smoke checks against a packed façade package — one
                                 project, an Android and an iOS head, same checks on both; the
                                 product is selected with -p:AgoraDeviceProduct=Video|Voice
samples/
  Net.Agora.Sample/             the MAUI sample app (Video)
  Net.Agora.Sample.Voice/       the MAUI sample app (Voice)
  Net.Agora.Sample.Signaling/   the MAUI sample app (Signaling — a tiny chat room)
assets/                         the package icon
Net.Agora.sln                   every project above except the sample, which consumes packed
                                 packages from ./artifacts and would break a plain restore
```

## Why two passes

No single .NET SDK can build .NET 8, 9 and 10 for a given platform — each SDK's workload carries
the current target framework and the previous one only:

| | SDK 9 band | SDK 10 band |
| --- | --- | --- |
| Android | `net8.0-android34.0`, `net9.0-android35.0` | `net10.0-android36.0` |
| iOS | `net8.0-ios18.0`, `net9.0-ios18.0` | `net10.0-ios26.0` |

So `BuildNugets.sh` packs each project twice — once under the SDK `global.json` pins, once from a
scratch directory whose own `global.json` selects the .NET 10 SDK — and `merge-packages.py`
copies the missing `lib/<tfm>` trees from the second package into the first, adding the matching
nuspec dependency groups. Verified for all three bands (net8/net9/net10) end to end: pack the
platform repos, copy their `.nupkg`s into this repository's `./artifacts`, then
`./build/BuildNugets.sh video`.

## Packing (this repository)

Requires the product's platform packages to already be resolvable — either from nuget.org once
published, or, for local testing before that, by copying their packed `.nupkg`s from the platform
repos' own `artifacts/` into this repository's `artifacts/` (`NuGet.config`'s `local-artifacts`
source):

```sh
# In sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS:
./build/BuildNugets.sh    # (iOS needs ./build/fetch-video.sh and ./build/fetch-voice.sh run first)

# Copy both repos' artifacts/*.nupkg into this repository's artifacts/, then:
./build/BuildNugets.sh video                          # the product's own version from Directory.Build.props
./build/BuildNugets.sh voice                          # same, for the Voice packages
./build/BuildNugets.sh signaling                      # same, for Net.Agora.Signaling
./build/BuildNugets.sh video --suffix beta.12.34      # prerelease: the product's version plus a suffix
```

There is no way to pass a whole version: the products sit on independent native version lines
(RTC 4.6.x, RTM 2.2.x), so each packs at its own pin and releases publish whatever the pins say —
see the release workflow.

Output lands in `./artifacts`, which `NuGet.config` exposes as a package source so the tests and
the sample app resolve the packages that were just built rather than whatever is on nuget.org.

## Testing

```sh
dotnet test tests/Net.Agora.UnitTests                                  # platform-neutral logic, no device
AGORA_ARTIFACTS=./artifacts dotnet test tests/Net.Agora.PackageTests   # asserts the packed .nupkg shape
```

`AGORA_ARTIFACTS` is optional if `artifacts/` is already at the repository root, which is where
`BuildNugets.sh` writes it; the test project resolves it relative to the directory holding
`global.json` either way. Native `.aar`/xcframework payload checks for the platform packages
themselves live in their own repositories' package tests, not here — this repository only asserts
that its own packages carry the right target frameworks and depend on the platform packages at an
*exact* pinned version (a floating range would silently resolve a different platform package than
the one a given façade build was verified against).

`Net.Agora.UnitTests` links the platform-neutral façade files (`AgoraVideoOptions`, the event-arg
types) directly rather than referencing `Net.Agora.Video` as a project — that project only
multi-targets `net*-android`/`net*-ios`, since `AgoraVideoClient`'s `JoinCore`/`LeaveCore`/
`DisposeCore` exist only in its `Platforms/Android` and `Platforms/Apple` halves. There is no
neutral head to build a plain `net9.0` test project against.

### Device checks

```sh
./.github/scripts/run-simulator-tests.sh <version> net9.0-ios18.0     # needs a booted simulator
./.github/scripts/run-emulator-tests.sh  <version> net9.0-android35.0 # needs a running emulator, adb
```

`Net.Agora.DeviceTests` (`tests/Net.Agora.DeviceTests`) drives `AgoraVideoClient` on a real engine:
constructs it, enables/disables video and audio, and joins a channel — which needs no real,
registered Agora App ID to be worth running. It uses a syntactically valid but unregistered one and
asserts the one thing true either way — the join fails within the configured timeout — alongside
the state machine around it (double-join guard, cancellation vs. timeout, dispose). See the remarks
on `SmokeTests` for why that is enough to run unconditionally, with no `AGORA_APP_ID` secret and
nothing to leak if one were passed in.

One project, an Android and an iOS head selected by `-p:AgoraDeviceTargetFramework=<tfm>` — the
same checks run unchanged on both, which is the point: if the façade did not actually unify
`RtcEngine` and `AgoraRtcEngineKit`, it could not compile for both heads, never mind pass on both.
It references the *packed* `Net.Agora.Video` from `./artifacts`, not a project reference, so it
exercises what actually ships.

## CI

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`build.yml`](../.github/workflows/build.yml) | called by the other two | Runs the unit tests, packs the Video façade packages, validates the package layout, runs the device checks on an iOS simulator and an Android emulator, builds the sample |
| [`pr.yml`](../.github/workflows/pr.yml) | pull requests | Builds `<version>-beta.<pr>.<run>` |
| [`release.yml`](../.github/workflows/release.yml) | `v*` tags | Publishes the tagged version and creates the GitHub release |

`unit-tests` runs on `ubuntu-latest` with no dependency on `pack`, since `Net.Agora.UnitTests`
needs neither a packed package nor a mobile workload — it is the fastest failure signal in the
pipeline. `pack` still needs a single `macos-15` runner: `Net.Agora.Video` targets both platforms'
TFMs, so restoring it needs the iOS workload even though nothing compiles Objective-C in this
repository. Pinned rather than `macos-latest` so an image roll cannot change which Xcode versions
are available to [`select-xcode.sh`](../.github/scripts/select-xcode.sh) — also needed by `e2e-ios`,
and because the sample's `net10.0-ios26.0` leg requires an Xcode carrying the matching iOS SDK.

`e2e-ios` and `e2e-android` each run as a matrix over `e2e-ios-target-frameworks` /
`e2e-android-target-frameworks` (default: the net8 and net10 extremes — see the comments on those
inputs in `build.yml`), driving [`run-simulator-tests.sh`](../.github/scripts/run-simulator-tests.sh)
/ [`run-emulator-tests.sh`](../.github/scripts/run-emulator-tests.sh) against the packages `pack`
just produced. `e2e-android` runs on `ubuntu-latest` rather than macOS — the emulator needs KVM,
which the macOS runners do not have, and the Android device-test head needs no Apple toolchain.

Publishing uses nuget.org [trusted publishing][trusted-publishing]: the job exchanges a GitHub
OIDC token for a short-lived API key, so there is no long-lived key in repository secrets. It needs
`id-token: write`, a `NUGET_USER` secret holding the nuget.org account name, and an `environment:`
matching the name recorded on the nuget.org policy (`nuget.org` in both workflows here). Policies
are scoped to a single workflow file, so `pr.yml` and `release.yml` each need their own — and each
of the three repositories needs its own set of policies, since they publish independently.

[trusted-publishing]: https://learn.microsoft.com/nuget/nuget-org/trusted-publishing
