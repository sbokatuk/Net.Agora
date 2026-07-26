# Release checklist — the ergonomics train

Everything below needs your credentials, so it is left for you: SSH pushes from this machine are
not authorised, and nuget.org deprecation has no API worth scripting.

All four repositories have their work on a branch named **`ergonomics`**, packed and tested locally.
Nothing is pushed.

## 1. Push the branches and open the pull requests

| Repository | Branch | Commits |
| --- | --- | --- |
| `Net.Agora.Android` | `ergonomics` | 5 |
| `Net.Agora.iOS` | `ergonomics` | 3 |
| `Net.Agora.Mac` | `ergonomics` | 2 |
| `Net.Agora` (façade) | `ergonomics` | 9 |

The façade's `build-samples` legs for a product go green only once that product's platform packages
are on nuget.org, which is the normal façade-after-platform ordering — so merge the three binding
repositories first.

## 2. Tag the binding repositories

Tags drive `release.yml`, which packs only the tagged track and publishes it (`build/tracks.tsv` →
`build/resolve-track.sh`). Notes come from `docs/release-notes/<tag>.md`, all of which are written.

**`Net.Agora.Android`** — whiteboard before fastboard, because `Net.Agora.Fastboard.Android`
depends on `Net.Agora.Whiteboard.Android` and a plain minimum version cannot resolve an
unpublished one:

```
v4.6.3.5                 # Video, Voice, 12 extensions
signaling-v2.2.6.2
chat-v1.4.0.2
iot-v1.3.0.2             # first publication of this package on the current generation
whiteboard-v2.16.123.2
fastboard-v1.8.1.2
```

**`Net.Agora.iOS`** — same ordering constraint for the two boards:

```
v4.6.2.5                 # Video, Voice, 12 extensions
signaling-v2.2.6.2
chat-v1.4.0.2
whiteboard-v2.16.137.2
fastboard-v1.4.5.2
```

**`Net.Agora.Mac`**:

```
v4.6.2.2                 # Video, Voice, 12 extensions
signaling-v2.2.8.2
```

## 3. Tag the façade, once the platform packages are live

The façade pins its platform packages at exact versions, so these tags cannot resolve until step 2
has published:

```
v4.6.2.5                 # Video + Voice, and their .Maui companions
signaling-v2.2.6.2       # includes Net.Agora.Signaling.Maui, never published before
chat-v1.4.0.2
whiteboard-v2.16.137.2   # first stable publication — only -beta.5.16 is on nuget.org
fastboard-v1.4.5.2       # first stable publication — only -beta.5.16 is on nuget.org
```

## 4. nuget.org housekeeping

### Deprecate and unlist the 2024 Xamarin-era packages

These predate the current generation, several carry copy-paste-wrong descriptions (the
`InteractiveWhiteboard` packages are described as "IoT SDK"), and they sit interleaved with the
current family in search results. Use **Manage → Deprecation** then **Manage → Listing** on each;
deprecation is what shows a warning and an alternate suggestion inside an IDE, unlisting alone only
hides it from search.

| Package | Versions | Alternate package to suggest |
| --- | --- | --- |
| `Net.Agora.InteractiveWhiteboard` | 2.16.62.1-beta1 | `Net.Agora.Whiteboard` |
| `Net.Agora.InteractiveWhiteboard.Android` | 2.16.59.1-beta1 | `Net.Agora.Whiteboard.Android` |
| `Net.Agora.InteractiveWhiteboard.iOS` | 2.16.62.1-beta1 | `Net.Agora.Whiteboard.iOS` |
| `Net.Agora.InteractiveWhiteboard.Mac` | 2.16.62.1-beta1 | none — netless publishes no native AppKit whiteboard; say so in the message |
| `Net.Agora.MediaplayerKit` | 1.3.0.1-beta1 | `Net.Agora.Video` — `IMediaPlayer` ships inside RTC 4.x |
| `Net.Agora.MediaplayerKit.Android` | 1.3.0.1-beta1 | `Net.Agora.Video.Android` |
| `Net.Agora.MediaplayerKit.iOS` | 1.3.0.1-beta1 | `Net.Agora.Video.iOS` |
| `Net.Agora.MediaplayerKit.Mac` | 1.3.0.1-beta1 | `Net.Agora.Video.Mac` |
| `Net.Agora.IoT` | 1.8.0.1-beta1 | `Net.Agora.IoT.Android` |
| `Net.Agora.IoT.iOS` | 1.0.0.1-beta1 | none — Agora ships no iOS IoT SDK; say so in the message |

### Optional

The pre-release versions of current package ids (`Net.Agora.Video.Android` 4.2.6.1-beta1,
`Net.Agora.Signaling` 2.1.7.1-beta1 and similar) still appear in each package's version dropdown.
Unlisting them tidies the history; nothing depends on them. Skip it if you want the history visible.

## 5. What to watch on the first CI run

- **`Net.Agora.Android` pack job** runs `build/verify-artifacts.sh` before the Maven cache is
  restored. If Agora or netless has re-published anything under a pinned coordinate since this
  branch was cut, that step fails by design and names the artifact — refresh the line with
  `./build/verify-artifacts.sh --print` after checking *why* the bytes changed.
- **`Net.Agora.Android` e2e** has a new leg that builds with R8 on (`AGORA_SHRINK=1`, the Signaling
  flavour). A failure there means keep rules, not a broken binding.
- **Façade `unit-tests`** now passes `-p:AgoraNeutralOnly=true`. Without it the job would need the
  mobile workloads, because restore evaluates every target framework of a referenced project.
- **Façade `build-samples`** macOS legs need the `Net.Agora.*.Mac` packages published (step 2).
