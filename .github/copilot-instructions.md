# Net.Agora — repository instructions

## Overview

- This is the umbrella façade of the Net.Agora family: one C# API over Agora's native SDKs, for
  .NET MAUI and for plain .NET for Android, iOS and macOS.
- **It binds nothing.** The raw bindings live in `sbokatuk/Net.Agora.Android`,
  `sbokatuk/Net.Agora.iOS` and `sbokatuk/Net.Agora.Mac`; every façade consumes them as an ordinary
  nuget.org `PackageReference` at an exact `[x.y.z.r]` pin.
- Six products, each a cross-platform client plus a `.Maui` companion — `Net.Agora.Video`,
  `.Voice`, `.Signaling`, `.Chat`, `.Whiteboard`, `.Fastboard` — 12 projects under `src/`.
- All pins live in `Directory.Build.props`. A product's own version is
  `<its iOS native line>.<binding revision>`: Video/Voice `4.6.2.5`, Signaling `2.2.6.2`, Chat
  `1.4.0.2`, Whiteboard `2.16.137.2`, Fastboard `1.4.5.2`. Android and iOS pins legitimately
  differ — Agora ships the two on independent schedules.
- Only Video, Voice and Signaling carry a `net*-macos` (AppKit) leg. There is no Mac Catalyst
  anywhere, and the `.Maui` packages are Android + iOS only.

## Build and verify

```sh
dotnet test tests/Net.Agora.UnitTests -p:AgoraNeutralOnly=true   # any OS, no workloads, fastest signal
./build/BuildNugets.sh video                                     # macOS only; one product -> ./artifacts
./build/BuildNugets.sh --track rtc --suffix beta.12.34           # a whole track, prerelease suffix
dotnet test tests/Net.Agora.PackageTests                         # after packing; reads artifacts/*.nupkg
```

- `global.json` pins .NET SDK 9.0.100; `BuildNugets.sh` also needs 10.0.100 — it packs a `net9`
  band and a `net10` band and merges them with `build/merge-packages.py`.
- Pack only on macOS: every façade lists the iOS TFMs, so restore needs the iOS workload even
  though nothing here compiles Objective-C.
- Packing a product needs its platform packages resolvable — from nuget.org, or from `artifacts/`
  (`NuGet.config`'s `local-artifacts` source) after packing the sibling repos locally.
- Device checks: `.github/scripts/run-simulator-tests.sh`, `run-emulator-tests.sh` and
  `run-macos-tests.sh`, each taking `<version> <target-framework> <Product>`.
- Read versions in shell by sourcing `build/pins.sh` — the only parser of `Directory.Build.props`.
  Never re-grep the props file.

## Layout

- `src/` — 12 façade projects; each client has `Platforms/{Android,Apple,Neutral}`.
- `tests/` — `Net.Agora.UnitTests` (net9.0 xUnit, project references, neutral legs),
  `Net.Agora.PackageTests` (net9.0 xUnit over the packed `.nupkg`s, `Xunit.SkippableFact` skips the
  Apple packages on Linux), `Net.Agora.DeviceTests` (`Platforms/{Android,iOS,Mac}`, one product per
  app via `-p:AgoraDeviceProduct=`, prints `AGORA_E2E_DONE PASS|FAIL`).
- `samples/` — 9 apps, deliberately outside `Net.Agora.sln`; they restore packed nupkgs from
  `artifacts/` at exact `[x.y.z.r]` versions.
- `build/` — `BuildNugets.sh`, `pins.sh`, `merge-packages.py`, `resolve-track.sh`, `tracks.tsv`,
  `upstream.tsv`, `check-upstream.sh`.
- `docs/` — `BUILD.md` (authoritative), `RELEASE-CHECKLIST-ergonomics.md`, `release-notes/`.
- `.github/workflows` and `.github/scripts`.

## Conventions

- Per product: `IAgora<P>Client` plus a `sealed partial Agora<P>Client` in the project root, with
  the platform halves in `Platforms/Android|Apple|Neutral` implementing private `*Core()` methods
  (`JoinCore`, `LeaveCore`, `DisposeCore`, …). macOS shares `Platforms/Apple` behind `#if MACOS`.
- Translate native callbacks in a nested `private sealed class Handler(...) : IRtcEngineEventHandler`
  (Android) or `private sealed class Delegate(...) : AgoraRtcEngineDelegate` (Apple), which call the
  shared half's `Raise*` methods.
- Await with `TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)` plus a
  timeout, never a blocking wait.
- The Neutral leg is plain `netX.0`, binds nothing, and its constructor throws
  `PlatformNotSupportedException`.
- `AgoraEngineSlot` (Video and Voice) guards the process-wide native engine — `Acquire()` before
  creating it, `Release()` on dispose or failed construction; it is `internal`, with
  `InternalsVisibleTo` for `Net.Agora.UnitTests`.
- Use British spelling in prose to match the README ("Licence", "colours").

## CI and release flow

- A PR runs `pr.yml` → `build.yml` (unit tests, pack on macOS, sample builds, package validation,
  iOS/macOS/Android device matrices) and publishes `-beta.<pr>.<run>` packages to nuget.org.
- Releasing: merge a PR adding `docs/release-notes/<tag>.md`; `auto-release.yml` tags that commit
  and dispatches `release.yml`, which guards that the tag is on the default branch, resolves the
  track with `build/resolve-track.sh`, then packs with `verify: false` and publishes.
- Tracks in `build/tracks.tsv`: `rtc` (bare `v*` → video + voice), `signaling-v*`, `chat-v*`,
  `whiteboard-v*`, `fastboard-v*`; longest-prefix match, so `v` never swallows `chat-v`.
- Releases skip verification on purpose — the tagged commit was verified on its pull request.
- Publishing uses nuget.org trusted publishing via OIDC; the only secret is `NUGET_USER`.
- `upstream-drift.yml` runs daily at 06:51 UTC and files one fingerprinted issue per group from
  `build/upstream.tsv`.

## Testing

- Three tiers: unit tests (neutral, any OS), package tests (packed nupkgs), device tests (real
  engine on simulator/emulator/host).
- Before opening a PR run, in order: unit tests, `./build/BuildNugets.sh <product>`, package tests.
- Device tiers run in CI matrices over the net8 and net10 extremes; macOS runs one leg only.

## Hard rules

- Never float a platform-package version. Pins are exact `[x.y.z.r]` and live only in
  `Directory.Build.props`.
- Pin stable versions only. The platform repos publish `-beta.<pr>.<run>` builds to nuget.org; a
  `-beta.*` pin must never reach a release-note merge or a tagged release.
- Never set `AgoraNeutralOnly=true` on a pack — it is test-only and silently produces packages with
  no platform legs.
- Never add a Mac Catalyst TFM, a macOS leg to Chat/Whiteboard/Fastboard, or a macOS leg to any
  `.Maui` project. Agora ships no such slices.
- Never commit native artifacts or binding code here — those belong to the platform repos.
- Never bypass the release path: tags must point at commits on the default branch that went through
  a PR, and `docs/release-notes/<tag>.md` drives auto-release.
- Never add samples to `Net.Agora.sln`, and keep them consuming packed nupkgs from `artifacts/`
  rather than project references.
- Keep `pr.yml`'s beta publish gated on same-repo pull requests — fork PRs cannot read
  `NUGET_USER`.
- Keep `CheckEolTargetFramework=false`: net8 ships deliberately.
- One RTC product per app: Video and Voice native artifacts collide.

## References

- `docs/BUILD.md` — authoritative build and architecture notes.
- Sibling repos: `sbokatuk/Net.Agora.Android`, `sbokatuk/Net.Agora.iOS`, `sbokatuk/Net.Agora.Mac`.
- Agora SDK documentation at docs.agora.io; netless for Whiteboard and Fastboard.

Trust these instructions and search the codebase only when something here is incomplete or wrong.
