#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against the packed Net.Agora.Video package, installs it on a running
# Android emulator and runs its checks. The app reports its verdict to logcat under a single tag;
# this script turns that into an exit code.
#
# Assumes an emulator is already booted and visible to adb - in CI that is
# reactivecircus/android-emulator-runner, locally it is whatever you started yourself.
#
# Usage: run-emulator-tests.sh VERSION [TARGET_FRAMEWORK] [PRODUCT]
#
# PRODUCT is Video (default), Voice, Signaling, Chat, Whiteboard or Fastboard — which façade package the suite
# consumes. One run
# exercises one product: their platform packages carry the same native artifacts, so a single
# app holds one of them.
#
# Environment:
#   AGORA_SHRINK=1          add R8 to the Release build/link check
#   AGORA_WITH_SIGNALING=1  also reference the Signaling façade (Video/Voice only)

VERSION="${1:?a package version is required}"
PRODUCT="${3:-Video}"
PRODUCT_LOWER="$(printf '%s' "${PRODUCT}" | tr '[:upper:]' '[:lower:]')"
TARGET_FRAMEWORK="${2:-net10.0-android36.0}"

PACKAGE_NAME="com.sbokatuk.agora.devicetests"
LOG_FILE="emulator-tests.log"
LOG_TAG="AgoraE2E"
# CI emulators are x86_64. Override for a local arm64 emulator on Apple silicon.
DEVICE_RID="${AGORA_DEVICE_RID:-android-x64}"
POLL_ATTEMPTS=90
POLL_INTERVAL=5

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/Net.Agora.DeviceTests/Net.Agora.DeviceTests.csproj"

# The SDK band is chosen by the *Android API level* in the target framework, not by the .NET
# version alone, because that is what decides which workload owns the runtime packs:
#
#   net8.0-android34.0  -> android 34.0.x, in the .NET 8 band
#   net9.0-android35.0  -> android 35.0.x, in the .NET 9 band
#   net10.0-android36.0 -> android 36.0.x, in the .NET 10 band
#
# The .NET 9 band compiles a net8 app happily - it has the API 34 *reference* packs - and then
# fails at packaging time, because it has no API 34 *runtime* packs and they cannot be restored
# from NuGet (NETSDK1112). The runtime packs come from the workload, not from a restore.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    net8.0-*)  sdk_major=8 ;;
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
# silently reuses the stale copy — Net.Agora.Video.Android's own cache entry too, not just the
# facade's, since its version does not change just because its content did (see
# NetAgoraVideoAndroidVersion in Directory.Build.props). See the longer version of this comment in
# run-simulator-tests.sh, where a stale extraction of the iOS platform package's cache was exactly
# how a missing native framework kept reappearing after being fixed and repacked.
. "${REPO_ROOT}/build/pins.sh"
rm -rf "${HOME}/.nuget/packages/net.agora.${PRODUCT_LOWER}/${VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.video.android/${NET_AGORA_VIDEO_ANDROID_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.voice.android/${NET_AGORA_VOICE_ANDROID_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.signaling.android/${NET_AGORA_SIGNALING_ANDROID_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.chat.android/${NET_AGORA_CHAT_ANDROID_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.whiteboard.android/${NET_AGORA_WHITEBOARD_ANDROID_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.fastboard.android/${NET_AGORA_FASTBOARD_ANDROID_VERSION}"

rm -rf "${REPO_ROOT}/tests/Net.Agora.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin"

# AGORA_SHRINK=1 adds R8 to the Release build/link check below. Off by default so the ordinary legs
# keep meaning "the façade and its payload survive trimming and AOT"; on, the leg additionally
# proves the platform packages' R8 keep rules travel through the façade into an app - the rules ship
# in the binding packages' buildTransitive/ and reach a consumer transitively, which is a path only
# this repository can exercise. Empty-array expansion is guarded for macOS's bash 3.2 under set -u.
SHRINK_ARGS=()
if [ "${AGORA_SHRINK:-0}" = "1" ]; then
    SHRINK_ARGS=(-p:AndroidLinkTool=r8)
fi

# AGORA_WITH_SIGNALING=1 additionally references the Signaling façade, without changing which suite
# compiles. Valid on the Video and Voice products only. Signaling and the RTC products each bring
# an aosl and every copy lands at lib/<abi>/libaosl.so, so the app keeps one and both products run
# against it — a mismatch there made RtcEngine.Create() return null on a device while the build said
# nothing. Nothing here held both products at once before, which is why this side never saw it.
COEXIST_ARGS=()
if [ "${AGORA_WITH_SIGNALING:-0}" = "1" ]; then
    COEXIST_ARGS=(-p:AgoraReferenceSignaling=true)
fi

echo "==> Release build/link check (version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version}, shrink=${AGORA_SHRINK:-0}, signaling=${AGORA_WITH_SIGNALING:-0})"
# A Release build as a build-and-link check only - not installed, not run. Release turns on the
# managed linker/trimmer (PublishTrimmed) and AOT (RunAOTCompilation), and this proves the Agora
# binding survives both and still produces an .apk, which a Debug build (neither on) never
# exercises.
#
# It does NOT cover R8 on its own: Release leaves AndroidLinkTool empty, so Java shrinking is off
# unless a project opts in - measured on a Release build of the sample, which produces no
# mapping.txt. AGORA_SHRINK=1 adds it (see below), which is what proves the keep rules the platform
# packages ship in buildTransitive/ reach an app through the façade.
#
# The check deliberately stops at the build and does not launch the app: the AOT image is compiled
# against the trimmed assembly set and disagrees with what the runtime loads, so a Release app
# aborts on startup before a single check runs - which is why the e2e run below is a separate Debug
# build.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Release \
    -p:AgoraDeviceProduct="${PRODUCT}" \
    -p:AgoraPackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${DEVICE_RID}" \
    ${SHRINK_ARGS[@]+"${SHRINK_ARGS[@]}"} \
    ${COEXIST_ARGS[@]+"${COEXIST_ARGS[@]}"} )

echo "==> building device tests for the e2e run (version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Debug for the run, for the reason above: a Release build aborts on startup, so the checks can
# only run against a Debug build. This is the build that gets installed and exercised.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:AgoraDeviceProduct="${PRODUCT}" \
    -p:AgoraPackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${DEVICE_RID}" \
    ${COEXIST_ARGS[@]+"${COEXIST_ARGS[@]}"} \
    -t:Install )

echo "==> granting camera/microphone permissions"
# Dangerous (runtime) permissions on Android 6+: declaring them in AndroidManifest.xml is not
# enough to have them granted. Without this, EnableVideo/MuteLocalAudio report a different failure
# than the one SmokeTests.cs isolates.
adb shell pm grant "${PACKAGE_NAME}" android.permission.CAMERA
adb shell pm grant "${PACKAGE_NAME}" android.permission.RECORD_AUDIO

echo "==> launching"
adb logcat -c
# The activity name is pinned in the app rather than left to the generated crc64* name, so this
# target stays stable across builds.
adb shell am start -n "${PACKAGE_NAME}/.MainActivity"

echo "==> waiting for the verdict"
for _ in $(seq "${POLL_ATTEMPTS}"); do
    if adb logcat -d -s "${LOG_TAG}:*" | grep -q "AGORA_E2E_DONE"; then
        break
    fi
    sleep "${POLL_INTERVAL}"
done

adb logcat -d -s "${LOG_TAG}:*" | tee "${LOG_FILE}"

if ! grep -q "AGORA_E2E_DONE PASS" "${LOG_FILE}"; then
    # No verdict usually means the app died before reporting, so keep the crash trace. A missing
    # Java dependency shows up here as a NoClassDefFoundError naming the class.
    echo "==> no passing verdict; capturing crash output"
    adb logcat -d -s AndroidRuntime:E DEBUG:F "${PACKAGE_NAME}:*" 2>/dev/null \
        | tail -100 | tee -a "${LOG_FILE}" || true
    echo "::error::Agora emulator checks failed or timed out"
    exit 1
fi

echo "==> emulator checks passed"
