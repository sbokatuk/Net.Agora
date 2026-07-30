---
applyTo: "src/**/*.cs"
description: Façade client conventions for the Net.Agora products.
---

# Façade conventions

- Give each product an `IAgora<P>Client` interface and a `sealed partial class Agora<P>Client` in
  the project root holding the shared logic — validation, state machine, events, `Dispose`.
- Put every native call in a platform half under `Platforms/Android`, `Platforms/Apple` or
  `Platforms/Neutral`, implementing private `*Core()` methods (`JoinCore`, `LeaveCore`,
  `DisposeCore`, `Set*Core`, …). The shared half calls `*Core()`; it never touches a native type.
- macOS reuses `Platforms/Apple`. Handle the differences with `#if MACOS` in that file rather than a
  fourth folder.
- Add a new member as a public method on the shared half plus one private `*Core()` per platform,
  including Neutral. Neutral must stay compilable — every `*Core()` it declares throws
  `PlatformNotSupportedException`, except `DisposeCore()`, which does nothing.
- Keep the Neutral constructor throwing `PlatformNotSupportedException` with a message naming the
  interface to program against and the heads that can construct the real client.
- Translate native callbacks into .NET events, never exceptions: use a nested
  `private sealed class Handler(Agora<P>Client owner) : IRtcEngineEventHandler` on Android and a
  `private sealed class Delegate(Agora<P>Client owner) : AgoraRtcEngineDelegate` on Apple, each
  calling the shared half's `Raise*` methods. Map native ints to the façade's own enums and record
  types; do not leak platform types across the seam.
- Make asynchronous operations awaitable with
  `new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)` plus a timeout
  and cancellation, so a native callback never resumes a caller inline.
- For the RTC products, `Acquire()` the `AgoraEngineSlot` immediately before creating the native
  engine and `Release()` it on dispose or when construction fails.

## Compile items and package references

- Keep `<Compile Remove="Platforms/**/*.cs" />` at the top of each csproj and add the platform's
  sources back per target platform identifier — `''` for Neutral, `android`, `ios`, `macos`. Never
  let the default glob compile one platform's half into another's assembly.
- Put the platform `PackageReference` in the same conditioned `ItemGroup` as its sources, always as
  an exact pin: `Version="[$(NetAgora<P><Platform>Version)]"`. Add the version property to
  `Directory.Build.props`, `build/pins.sh` and `build/upstream.tsv`.
- Build `TargetFrameworks` from the band properties (`$(AgoraNeutralTargetFrameworks)`,
  `$(AgoraAndroidTargetFrameworks)`, `$(AgoraIosTargetFrameworks)`,
  `$(AgoraMacosTargetFrameworks)`), never from literal TFMs. `.Maui` projects list Android and iOS
  only and use `UseMauiCore`, not `UseMaui`.
- Grant `InternalsVisibleTo Net.Agora.UnitTests` when a test needs internal state; do not widen
  visibility to `public` for testing.
- Document every public member with XML docs — `GenerateDocumentationFile` is on.
