#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against the packed Net.Agora.Video package, installs it on an iOS
# simulator and runs its checks. The app prints its verdict to stdout; this script turns that into
# an exit code.
#
# Usage: run-simulator-tests.sh VERSION [TARGET_FRAMEWORK] [PRODUCT]
#
# PRODUCT is Video (default), Voice, Signaling, Chat, Whiteboard or Fastboard — which façade package the suite
# consumes. One run
# exercises one product: their platform packages carry the same native artifacts, so a single
# app holds one of them.

VERSION="${1:?a package version is required}"
PRODUCT="${3:-Video}"
PRODUCT_LOWER="$(printf '%s' "${PRODUCT}" | tr '[:upper:]' '[:lower:]')"
TARGET_FRAMEWORK="${2:-net10.0-ios26.0}"

BUNDLE_ID="com.sbokatuk.agora.devicetests"
LOG_FILE="simulator-tests.log"
# CI runners are Apple silicon. Override for an Intel runner, whose simulator is x64.
SIMULATOR_RID="${AGORA_SIMULATOR_RID:-iossimulator-arm64}"
SIMULATOR_DEVICE="${AGORA_SIMULATOR_DEVICE:-iPhone 17}"

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/Net.Agora.DeviceTests/Net.Agora.DeviceTests.csproj"

# The .NET 9 band builds net8/net9 and the .NET 10 band builds net9/net10, so pick the SDK that owns
# the requested target framework. The SDK is resolved from the working directory, and the
# repository's global.json pins .NET 9, hence the scratch directory.
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
# silently reuses the stale copy. CI versions are unique, but locally you will re-pack the same
# version repeatedly and test yesterday's bits without this — Net.Agora.Video.iOS's cache entry
# needs clearing too, not just the facade's: the platform package's own version does not change
# just because its content did (see NetAgoraVideoIosVersion in Directory.Build.props), so a stale
# extraction there is exactly how a missing native framework kept reappearing after being fixed and
# repacked in sbokatuk/Net.Agora.iOS during this suite's own development.
. "${REPO_ROOT}/build/pins.sh"
rm -rf "${HOME}/.nuget/packages/net.agora.${PRODUCT_LOWER}/${VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.video.ios/${NET_AGORA_VIDEO_IOS_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.voice.ios/${NET_AGORA_VOICE_IOS_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.signaling.ios/${NET_AGORA_SIGNALING_IOS_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.chat.ios/${NET_AGORA_CHAT_IOS_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.whiteboard.ios/${NET_AGORA_WHITEBOARD_IOS_VERSION}"
rm -rf "${HOME}/.nuget/packages/net.agora.fastboard.ios/${NET_AGORA_FASTBOARD_IOS_VERSION}"

# The app's own intermediate output has to go too, not just the NuGet cache. The iOS package's
# native payload is extracted out of the package into obj/ and copied into the .app, and neither
# step re-runs when the package version string is unchanged - so a rebuilt package of the same
# version leaves the *previous* build's xcframeworks embedded in the app.
rm -rf "${REPO_ROOT}/tests/Net.Agora.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin"

echo "==> building device tests (version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Release, so the checks run against the same trimmed and AOT-compiled build a consumer ships - the
# configuration where a binding that restores fine in Debug can still fail to link. It costs more
# runner time than Debug, but unlike the Android emulator suite an iOS simulator Release build
# still starts and runs, so this stays a single build-and-run rather than a split build/link check.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Release \
    -p:AgoraDeviceProduct="${PRODUCT}" \
    -p:AgoraPackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${SIMULATOR_RID}" )

APP_PATH="$(find "${REPO_ROOT}/tests/Net.Agora.DeviceTests/bin/Release/${TARGET_FRAMEWORK}/${SIMULATOR_RID}" \
    -maxdepth 1 -name '*.app' -print -quit)"
if [ -z "${APP_PATH}" ]; then
    echo "::error::no .app bundle was produced"
    exit 1
fi

echo "==> selecting simulator"
# Prefer the requested device, but fall back to any available iPhone rather than failing: which
# device names exist depends on the installed Xcode, and pinning one couples this script to a
# runner image that changes without notice. Newest runtime first, so the picked device is the most
# current one available.
#
# The device name goes to python through the environment rather than being interpolated into the
# script text: macOS still ships bash 3.2, so the ${VAR@Q} quoting operator is unavailable, and an
# unquoted name containing a space would corrupt the program.
selection="$(xcrun simctl list devices available --json \
    | AGORA_PREFERRED_DEVICE="${SIMULATOR_DEVICE}" python3 -c "
import json, os, sys

preferred = os.environ['AGORA_PREFERRED_DEVICE']
runtimes = json.load(sys.stdin)['devices']

def candidates():
    for runtime in sorted(runtimes, reverse=True):
        for device in runtimes[runtime]:
            yield device

for device in candidates():
    if device['name'] == preferred:
        print(device['udid'], device['name'], sep='\t')
        raise SystemExit

for device in candidates():
    if device['name'].startswith('iPhone'):
        print(device['udid'], device['name'], sep='\t')
        raise SystemExit
")"

udid="${selection%%$'\t'*}"
device_name="${selection#*$'\t'}"

if [ -z "${udid}" ]; then
    echo "::error::no available iPhone simulator to run on"
    xcrun simctl list devices available
    exit 1
fi

if [ "${device_name}" != "${SIMULATOR_DEVICE}" ]; then
    echo "==> '${SIMULATOR_DEVICE}' is not available, using '${device_name}'"
fi

echo "==> booting ${device_name} (${udid})"
xcrun simctl boot "${udid}" 2>/dev/null || true
xcrun simctl bootstatus "${udid}" -b

echo "==> installing"
xcrun simctl install "${udid}" "${APP_PATH}"

echo "==> running"
# --console-pty streams the app's stdout straight back, so the verdict rarely needs log scraping.
# The app terminates itself once it has printed AGORA_E2E_DONE.
set +e
xcrun simctl launch --console-pty "${udid}" "${BUNDLE_ID}" 2>&1 | tee "${LOG_FILE}"
set -e

if ! grep -q "AGORA_E2E_DONE PASS" "${LOG_FILE}"; then
    # No verdict usually means the app died before reporting, so keep the crash trace. A missing or
    # mis-stripped xcframework shows up here as a dyld failure naming the framework.
    echo "==> no passing verdict on the console stream; capturing the device log"
    device_log="${SDK_DIR}/device.log"
    xcrun simctl spawn "${udid}" log show --last 2m --predicate "process == 'Net.Agora.DeviceTests'" \
        2>/dev/null > "${device_log}" || true

    # The stream can also lose its tail while the app runs on to its verdict, so a stream with no
    # verdict at all is settled by the device log. That log holds earlier runs too, so only a line
    # from this launch counts, matched on the pid in column 6. simctl prints the pid as it exits, so
    # a killed simctl leaves none and the run fails; its exit status can't tell, being 0 after a
    # crash or a SIGTERM. Read all of this before the dump below lands in the same file, and from
    # the whole log, because the verdict can sit more than 100 lines from its end.
    log_verdict=""
    if ! grep -q "AGORA_E2E_DONE" "${LOG_FILE}"; then
        launched_pid="$(LC_ALL=C sed -n "s/.*${BUNDLE_ID}: \([0-9][0-9]*\)$/\1/p" "${LOG_FILE}" | tail -1)"
        if [ -n "${launched_pid}" ]; then
            log_verdict="$(LC_ALL=C awk -v pid="${launched_pid}" '$6 == pid && /AGORA_E2E_DONE/' "${device_log}")"
        fi
    fi

    tail -100 "${device_log}" | tee -a "${LOG_FILE}" || true

    if [[ "${log_verdict}" == *"AGORA_E2E_DONE PASS"* && "${log_verdict}" != *"AGORA_E2E_DONE FAIL"* ]]; then
        echo "${log_verdict}" | tee -a "${LOG_FILE}"
        echo "::warning::simulator checks passed, but the console stream lost the verdict, so it was read from the device log"
    else
        echo "::error::Agora simulator checks failed or timed out"
        exit 1
    fi
fi

echo "==> simulator checks passed"
