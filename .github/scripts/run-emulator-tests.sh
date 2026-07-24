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
# PRODUCT is Video (default), Voice, Signaling, Chat or Whiteboard — which façade package the suite
# consumes. One run
# exercises one product: their platform packages carry the same native artifacts, so a single
# app holds one of them.

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

rm -rf "${REPO_ROOT}/tests/Net.Agora.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin"

echo "==> building device tests (version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Debug, not Release. Release AOT-compiles every assembly, and an AOT image built against an
# unlinked assembly set disagrees with what the runtime loads - the app aborts on startup before a
# single check runs. Debug also skips the R8 shrinking this app has to avoid anyway.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:AgoraDeviceProduct="${PRODUCT}" \
    -p:AgoraPackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${DEVICE_RID}" \
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
