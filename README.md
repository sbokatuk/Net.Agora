# Net.Agora

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![Platforms: Android | iOS](https://img.shields.io/badge/platforms-Android%20%7C%20iOS-blue)](#packages)

.NET bindings for [Agora][agora]'s native SDKs, with one API across Android and iOS. Join RTC
channels, publish and subscribe audio and video, from C# — in .NET MAUI or plain .NET for Android
/ .NET for iOS.

Built on [Net.Agora.iOS](https://github.com/sbokatuk/Net.Agora.iOS) and
[Net.Agora.Android](https://github.com/sbokatuk/Net.Agora.Android), which bind the native SDKs.
This repository binds nothing itself — see [How this repository works](#how-this-repository-works).

```sh
dotnet add package Net.Agora.Video.Maui   # MAUI video apps: adds the video view
dotnet add package Net.Agora.Video        # video, everything else
dotnet add package Net.Agora.Voice.Maui   # MAUI voice-only apps
dotnet add package Net.Agora.Voice        # voice-only, everything else
dotnet add package Net.Agora.Signaling    # realtime messaging (RTM) — MAUI or plain, same package
```

```csharp
var client = new AgoraVideoOptions { AppId = "your-app-id" }.CreateClient();

client.SetLocalView(LocalView);          // a <agora:AgoraVideoView /> from your XAML
client.UserJoined += (_, e) => client.SetRemoteView(e.Uid, RemoteView);

await client.JoinAsync("my-channel");    // completes when the server confirms
```

Both clients carry the call essentials — speakerphone routing, who-is-speaking reports, remote
mute state, connection lifecycle events, token renewal — and the video client adds the camera
half (preview, flip, remote video mute). Voice-only apps get all of it over native artifacts that
carry no video codecs at all:

```csharp
var client = new AgoraVoiceOptions { AppId = "your-app-id", DefaultToSpeakerphone = true }.CreateClient();

client.VolumeIndication += (_, e) => ShowSpeakers(e.Speakers);   // uid 0 is you
client.EnableVolumeIndication(TimeSpan.FromMilliseconds(200));

await client.JoinAsync("my-room");
client.SetSpeakerphone(false);           // switch the live route mid-call
```

Signaling is the third product — login, channel subscribe, publish/receive — with the same
awaitable shape; it has no MAUI companion because it needs no platform glue at all:

```csharp
var signaling = new AgoraSignalingClient(new AgoraSignalingOptions
{
    AppId = "your-app-id",
    UserId = "alice",
});

signaling.MessageReceived += (_, e) => Show($"{e.Publisher}: {e.Text}");

await signaling.LoginAsync();
await signaling.SubscribeAsync("lobby");
await signaling.PublishAsync("lobby", "hi all");
```

Pick one RTC product per app: Video already carries the full audio surface, and the two RTC
products' native artifacts collide (same Java classes on Android, same `AgoraRtcKit` framework on
iOS). Signaling coexists with either.

## Status

This repository covers Agora's Video and Voice SDKs, wired end to end: the raw Android/iOS
bindings (in the two sibling repositories above), the cross-platform clients, the MAUI packages,
package tests, sample apps, CI. See [docs/BUILD.md](docs/BUILD.md) for the exact state.

| Product | Android | iOS | Cross-platform client | MAUI |
| --- | --- | --- | --- | --- |
| Video | ✅ [Net.Agora.Android](https://github.com/sbokatuk/Net.Agora.Android) | ✅ [Net.Agora.iOS](https://github.com/sbokatuk/Net.Agora.iOS) | ✅ | ✅ view + glue |
| Voice | ✅ [Net.Agora.Android](https://github.com/sbokatuk/Net.Agora.Android) | ✅ [Net.Agora.iOS](https://github.com/sbokatuk/Net.Agora.iOS) | ✅ | ✅ glue (no view — voice renders nothing) |
| Signaling | ✅ [Net.Agora.Android](https://github.com/sbokatuk/Net.Agora.Android) | ✅ [Net.Agora.iOS](https://github.com/sbokatuk/Net.Agora.iOS) | ✅ | n/a — no glue needed, same package everywhere |

## How this repository works

Three repositories, each independently versioned and released:

- **[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android)** — the raw Android
  bindings (`Agora.Rtc.*`, generated from `io.agora.rtc:full-rtc-basic` / `voice-rtc-basic`).
- **[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS)** — the raw iOS bindings
  (`Net.Agora.Video.iOS.*` / `Net.Agora.Voice.iOS.*`, hand-written against `AgoraRtcEngineKit`).
- **`Net.Agora`** (this repository) — the façades. Each façade package depends on its two platform
  packages at an exact pinned version (`NetAgora<Product><Platform>Version` in
  `Directory.Build.props`) the same way any consumer would, restored from nuget.org. It contains no
  `ApiDefinition.cs`, no native artifact, no `AndroidLibrary`/`NativeReference` — see
  [docs/BUILD.md](docs/BUILD.md).

## Without MAUI

`Net.Agora.Video.Maui` only adds the video view and the platform glue. A plain .NET for Android or
.NET for iOS app needs none of that — reference `Net.Agora.Video` and use the same
`AgoraVideoClient`, with the platform's own view type instead of `AgoraVideoView`:

```sh
dotnet add package Net.Agora.Video
```

**Android** — the SDK renders into a plain `Android.Views.SurfaceView`, and the client needs the
`Context`:

```csharp
using Net.Agora.Video;
using Android.App;
using Android.OS;
using Android.Views;

[Activity(Label = "Publisher", MainLauncher = true)]
public class MainActivity : Activity
{
    private AgoraVideoClient? _client;
    private SurfaceView? _preview;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _preview = new SurfaceView(this);
        SetContentView(_preview);

        var options = new AgoraVideoOptions { AppId = "your-app-id" };

        // The Context is the one thing the Android SDK cannot work out for itself.
        // Net.Agora.Video.Maui is what supplies it in a MAUI app.
        _client = new AgoraVideoClient(options, this);
        _client.SetLocalView(_preview);
        _client.EnableVideo();

        _client.UserJoined += (_, e) => Log.Info("app", $"remote user joined: {e.Uid}");
        _client.Error += (_, e) => Log.Error("app", e.Message);

        await _client.JoinAsync("my-channel");
    }

    protected override void OnDestroy()
    {
        // Releases the camera and the engine; managed collection alone does not.
        _client?.Dispose();
        base.OnDestroy();
    }
}
```

**iOS** — a plain `UIView`, and the client takes no platform handle at all:

```csharp
using System;
using Net.Agora.Video;
using UIKit;

public class ChannelViewController : UIViewController
{
    private AgoraVideoClient? _client;

    public override async void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);

        var options = new AgoraVideoOptions { AppId = "your-app-id" };

        _client = new AgoraVideoClient(options);

        // Any UIView will do — the SDK adds its own renderer as a subview.
        _client.SetLocalView(View!);
        _client.EnableVideo();

        _client.UserJoined += (_, e) => Console.WriteLine($"remote user joined: {e.Uid}");
        _client.Error += (_, e) => Console.WriteLine($"error: {e.Message}");

        await _client.JoinAsync("my-channel");
    }

    public override void ViewDidDisappear(bool animated)
    {
        _client?.Dispose();
        _client = null;
        base.ViewDidDisappear(animated);
    }
}
```

Both snippets create the client once the view exists rather than in a constructor — on Android the
`Context` route (`Platform.CurrentActivity`) is not reliably usable before `OnCreate`, and on Apple
the view has no window before `ViewDidAppear`.

Capture permissions are the app's own responsibility either way: `CAMERA` and `RECORD_AUDIO` in
`AndroidManifest.xml`, `NSCameraUsageDescription` and `NSMicrophoneUsageDescription` in
`Info.plist`.

## Packages

| Package | What it is | Target frameworks | Published from |
| --- | --- | --- | --- |
| `Net.Agora.Video.Maui` | MAUI video view and handlers | net8.0, net9.0, net10.0 (android + ios) | this repo |
| `Net.Agora.Video` | The cross-platform client: `IAgoraVideoClient`, options, events, async | net8.0, net9.0, net10.0 (android + ios) | this repo |
| `Net.Agora.Video.Android` | The raw binding to Agora's Video Android SDK | `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0` | [Net.Agora.Android](https://github.com/sbokatuk/Net.Agora.Android) |
| `Net.Agora.Video.iOS` | The raw binding to Agora's Video iOS SDK | `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0` | [Net.Agora.iOS](https://github.com/sbokatuk/Net.Agora.iOS) |

Each package pulls in the one below it, so a single reference is enough. Drop to a platform
binding directly for anything the cross-platform API does not expose — the full bound surface is
under `Agora.Rtc.*` (Android) and `Net.Agora.Video.iOS.*` (a hand-written subset — see each
repository's own README).

No Mac Catalyst: Agora's iOS SDK ships no `maccatalyst` slice (`ios-arm64` and simulator only).

## Why there is a cross-platform layer

Android's `RtcEngine` is a Java abstract class overridden with a 100+ method event-handler class;
iOS's `AgoraRtcEngineKit` takes an Objective-C delegate. Written against the bindings directly, an
app targeting both needs a real per-platform adapter before it can join a channel.
`Net.Agora.Video` is that adapter, so you do not write it.

The sample is the evidence: [`samples/Net.Agora.Sample`](samples) joins a channel and renders
local/remote video with no per-platform code at all.

## Building

See [docs/BUILD.md](docs/BUILD.md). In short, the platform packages are built in their own
repositories:

```sh
# In sbokatuk/Net.Agora.Android:
./build/BuildNugets.sh

# In sbokatuk/Net.Agora.iOS:
./build/fetch-video.sh
./build/BuildNugets.sh
```

Then, in this repository, pointing `NuGet.config`'s `local-artifacts` source at wherever those two
were packed (or once both are published, straight from nuget.org):

```sh
./build/BuildNugets.sh video
```

## Licence

MIT — see [LICENSE](LICENSE). The bundled Agora SDKs are Agora's own, distributed under their own
terms via Maven Central and CocoaPods respectively.

[agora]: https://www.agora.io/en/
