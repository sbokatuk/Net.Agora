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
