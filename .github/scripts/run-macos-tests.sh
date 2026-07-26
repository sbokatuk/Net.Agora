#!/usr/bin/env bash
set -euo pipefail

# Builds the façade device test app against a packed Net.Agora.<PRODUCT> package for its native
# macOS (AppKit) leg and runs it directly on the host Mac. The app prints its verdict to stdout;
# this script turns that into an exit code.
#
# Adapted from run-simulator-tests.sh in this repository — the app prints the same AGORA_E2E_DONE
# marker — with the simulator steps dropped: macOS has none, so the built .app's binary is executed
# straight on the runner, the way sbokatuk/Net.Agora.Mac's own suite does.
#
# Why this exists: the Video, Voice and Signaling façades carry a net*-macos leg that pulls
# Net.Agora.<Product>.Mac, and until this script nothing exercised it at runtime. The sample build
# proves that leg links; only these checks prove the packaged macOS payload loads and the
# cross-platform client actually drives it.
#
# Usage: run-macos-tests.sh VERSION [TARGET_FRAMEWORK] [PRODUCT]
#
# PRODUCT is Video (default), Voice or Signaling. The other three façades have no macOS leg — Agora
# ships no native macOS SDK for Chat, netless none for Whiteboard/Fastboard — and the app's csproj
# fails with that message rather than a restore error if one is asked for.

VERSION="${1:?a package version is required}"
TARGET_FRAMEWORK="${2:-net10.0-macos26.0}"
PRODUCT="${3:-Video}"

LOG_FILE="macos-tests.log"

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/Net.Agora.DeviceTests/Net.Agora.DeviceTests.csproj"
PRODUCT_LOWER="$(printf '%s' "${PRODUCT}" | tr '[:upper:]' '[:lower:]')"

case "${PRODUCT}" in
    Video|Voice|Signaling) ;;
    *)
        echo "::error::PRODUCT must be Video, Voice or Signaling — '${PRODUCT}' has no macOS leg" >&2
        exit 1
        ;;
esac

# The .NET 9 band builds net8/net9 and the .NET 10 band builds net9/net10, so pick the SDK that owns
# the requested target framework. The SDK is resolved from the working directory, and this
# repository's global.json pins .NET 9, hence the scratch directory below.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    *)         sdk_major=9 ;;
esac

sdk_version="$(dotnet --list-sdks | grep "^${sdk_major}\." | tail -1 | cut -d' ' -f1)"
if [ -z "${sdk_version}" ]; then
    echo "::error::no .NET ${sdk_major} SDK installed, cannot build ${TARGET_FRAMEWORK}"
    exit 1
fi

SDK_DIR="$(mktemp -d)"
trap 'rm -rf "${SDK_DIR}"' EXIT
printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "${sdk_version}" \
    > "${SDK_DIR}/global.json"

# NuGet caches by package id + version, so rebuilding a version that was already restored once
# silently reuses the stale copy. The platform package's cache entry needs clearing too, not just
# the façade's: its version does not change just because its content did (see the NetAgora*MacVersion
# pins in Directory.Build.props), and a stale extraction there is exactly how a missing native
# framework survives a fix-and-repack.
. "${REPO_ROOT}/build/pins.sh"
rm -rf "${HOME}/.nuget/packages/net.agora.${PRODUCT_LOWER}/${VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.video.mac/${NET_AGORA_VIDEO_MAC_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.voice.mac/${NET_AGORA_VOICE_MAC_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.signaling.mac/${NET_AGORA_SIGNALING_MAC_VERSION}"

# The app's own intermediate output has to go too, not just the NuGet cache: the native payload is
# extracted out of the package into obj/ and copied into the .app, and neither step re-runs when the
# package version string is unchanged.
rm -rf "${REPO_ROOT}/tests/Net.Agora.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin"

# Local escape hatch: .NET for macOS insists on the exact Xcode its SDK was built against. CI selects
# that Xcode (see select-xcode.sh), so this is empty there; a developer on a newer Xcode can set
# AGORA_SKIP_XCODE_CHECK=1 to build anyway — the check is major.minor only and a newer one links
# fine for a smoke run.
XCODE_OVERRIDE=""
if [ -n "${AGORA_SKIP_XCODE_CHECK:-}" ]; then
    XCODE_OVERRIDE="-p:_IsMatchingXcode=true"
fi

echo "==> building device tests (product=${PRODUCT}, version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Debug, not Release: a Release build AOT-compiles and links every assembly, which costs real runner
# time for no extra signal on what this suite verifies — that the packaged macOS payload restores,
# resolves and drives. The Release/link configuration is covered by the sample build legs.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:AgoraDeviceProduct="${PRODUCT}" \
    -p:AgoraPackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    ${XCODE_OVERRIDE} )

# The .app lands under an architecture-named subfolder (osx-arm64 on Apple silicon runners). Find
# the executable inside it rather than pinning the RID, so the same script works on any runner.
APP_BINARY="$(find "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin/Debug/${TARGET_FRAMEWORK}" \
    -type f -path '*.app/Contents/MacOS/*' -perm +111 -print -quit 2>/dev/null || true)"
if [ -z "${APP_BINARY}" ]; then
    echo "::error::no .app bundle was produced"
    exit 1
fi

echo "==> running ${APP_BINARY}"
# The app writes its checks to stdout and exits itself once it has printed AGORA_E2E_DONE. Quieten
# the Mono logger so the stream is the test output rather than runtime chatter.
set +e
MONO_LOG_LEVEL=error "${APP_BINARY}" 2>&1 | tee "${LOG_FILE}"
status=${PIPESTATUS[0]}
set -e

if ! grep -q "AGORA_E2E_DONE PASS" "${LOG_FILE}"; then
    # A missing or mis-stripped xcframework shows up here as a dyld failure naming the framework.
    echo "::error::Agora macOS checks failed or timed out (exit ${status})"
    exit 1
fi

echo "==> macOS checks passed"
