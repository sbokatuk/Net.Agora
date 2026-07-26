#!/usr/bin/env bash
# The only parser of Directory.Build.props for shell callers. Source this, don't execute it.
#
#   . build/pins.sh
#   echo "$NET_AGORA_VIDEO_ANDROID_VERSION"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AGORA_REPO_ROOT
AGORA_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

_prop() {
    grep -oE "<$1>[^<]+" "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed -E "s/<$1>//"
}

# Pinned versions of the platform packages the façades depend on — published from the
# sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS repositories, not built here.
export NET_AGORA_VIDEO_ANDROID_VERSION; NET_AGORA_VIDEO_ANDROID_VERSION="$(_prop NetAgoraVideoAndroidVersion)"
export NET_AGORA_VIDEO_IOS_VERSION; NET_AGORA_VIDEO_IOS_VERSION="$(_prop NetAgoraVideoIosVersion)"
export NET_AGORA_VOICE_ANDROID_VERSION; NET_AGORA_VOICE_ANDROID_VERSION="$(_prop NetAgoraVoiceAndroidVersion)"
export NET_AGORA_VOICE_IOS_VERSION; NET_AGORA_VOICE_IOS_VERSION="$(_prop NetAgoraVoiceIosVersion)"
export NET_AGORA_SIGNALING_ANDROID_VERSION; NET_AGORA_SIGNALING_ANDROID_VERSION="$(_prop NetAgoraSignalingAndroidVersion)"
export NET_AGORA_SIGNALING_IOS_VERSION; NET_AGORA_SIGNALING_IOS_VERSION="$(_prop NetAgoraSignalingIosVersion)"
# The native macOS (AppKit) bindings, from sbokatuk/Net.Agora.Mac. Only these three products have a
# macOS leg; run-macos-tests.sh clears their cache entries before a run for the same reason the iOS
# script clears its own — a platform package's version does not change just because its content did.
export NET_AGORA_VIDEO_MAC_VERSION; NET_AGORA_VIDEO_MAC_VERSION="$(_prop NetAgoraVideoMacVersion)"
export NET_AGORA_VOICE_MAC_VERSION; NET_AGORA_VOICE_MAC_VERSION="$(_prop NetAgoraVoiceMacVersion)"
export NET_AGORA_SIGNALING_MAC_VERSION; NET_AGORA_SIGNALING_MAC_VERSION="$(_prop NetAgoraSignalingMacVersion)"
export NET_AGORA_CHAT_ANDROID_VERSION; NET_AGORA_CHAT_ANDROID_VERSION="$(_prop NetAgoraChatAndroidVersion)"
export NET_AGORA_CHAT_IOS_VERSION; NET_AGORA_CHAT_IOS_VERSION="$(_prop NetAgoraChatIosVersion)"
export NET_AGORA_WHITEBOARD_ANDROID_VERSION; NET_AGORA_WHITEBOARD_ANDROID_VERSION="$(_prop NetAgoraWhiteboardAndroidVersion)"
export NET_AGORA_WHITEBOARD_IOS_VERSION; NET_AGORA_WHITEBOARD_IOS_VERSION="$(_prop NetAgoraWhiteboardIosVersion)"
export NET_AGORA_FASTBOARD_ANDROID_VERSION; NET_AGORA_FASTBOARD_ANDROID_VERSION="$(_prop NetAgoraFastboardAndroidVersion)"
export NET_AGORA_FASTBOARD_IOS_VERSION; NET_AGORA_FASTBOARD_IOS_VERSION="$(_prop NetAgoraFastboardIosVersion)"

# Each product's own version (tracks its iOS platform package's native version component — see
# the comment in Directory.Build.props).
export AGORA_VIDEO_IOS_VERSION; AGORA_VIDEO_IOS_VERSION="$(_prop AgoraVideoIosVersion)"
export AGORA_VOICE_IOS_VERSION; AGORA_VOICE_IOS_VERSION="$(_prop AgoraVoiceIosVersion)"
export AGORA_BINDING_REVISION; AGORA_BINDING_REVISION="$(_prop AgoraBindingRevision)"
export AGORA_VIDEO_PACKAGE_VERSION="${AGORA_VIDEO_IOS_VERSION}.${AGORA_BINDING_REVISION}"
export AGORA_VOICE_PACKAGE_VERSION="${AGORA_VOICE_IOS_VERSION}.${AGORA_BINDING_REVISION}"
export AGORA_SIGNALING_IOS_PRODUCT_VERSION; AGORA_SIGNALING_IOS_PRODUCT_VERSION="$(_prop AgoraSignalingIosVersion)"
export AGORA_SIGNALING_BINDING_REVISION; AGORA_SIGNALING_BINDING_REVISION="$(_prop AgoraSignalingBindingRevision)"
export AGORA_SIGNALING_PACKAGE_VERSION="${AGORA_SIGNALING_IOS_PRODUCT_VERSION}.${AGORA_SIGNALING_BINDING_REVISION}"
export AGORA_CHAT_IOS_PRODUCT_VERSION; AGORA_CHAT_IOS_PRODUCT_VERSION="$(_prop AgoraChatIosVersion)"
export AGORA_CHAT_BINDING_REVISION; AGORA_CHAT_BINDING_REVISION="$(_prop AgoraChatBindingRevision)"
export AGORA_CHAT_PACKAGE_VERSION="${AGORA_CHAT_IOS_PRODUCT_VERSION}.${AGORA_CHAT_BINDING_REVISION}"
export AGORA_WHITEBOARD_IOS_PRODUCT_VERSION; AGORA_WHITEBOARD_IOS_PRODUCT_VERSION="$(_prop AgoraWhiteboardIosVersion)"
export AGORA_WHITEBOARD_BINDING_REVISION; AGORA_WHITEBOARD_BINDING_REVISION="$(_prop AgoraWhiteboardBindingRevision)"
export AGORA_WHITEBOARD_PACKAGE_VERSION="${AGORA_WHITEBOARD_IOS_PRODUCT_VERSION}.${AGORA_WHITEBOARD_BINDING_REVISION}"
export AGORA_FASTBOARD_IOS_PRODUCT_VERSION; AGORA_FASTBOARD_IOS_PRODUCT_VERSION="$(_prop AgoraFastboardIosVersion)"
export AGORA_FASTBOARD_BINDING_REVISION; AGORA_FASTBOARD_BINDING_REVISION="$(_prop AgoraFastboardBindingRevision)"
export AGORA_FASTBOARD_PACKAGE_VERSION="${AGORA_FASTBOARD_IOS_PRODUCT_VERSION}.${AGORA_FASTBOARD_BINDING_REVISION}"
