#!/usr/bin/env bash
set -euo pipefail

# Packs the Net.Agora façade packages for a product — the cross-platform client and, where one
# exists, its MAUI package. Does NOT build any raw platform binding: those are published from
# sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS, and must already be resolvable — either
# from nuget.org, or from this repository's ./artifacts (NuGet.config's local-artifacts source),
# which is where you'd copy their own build/BuildNugets.sh output for local testing.
#
# Usage:
#   ./build/BuildNugets.sh video                         # the product's own version from Directory.Build.props
#   ./build/BuildNugets.sh voice --suffix beta.12.34     # same, with a prerelease suffix appended
#
# Each product packs at its own <VersionPrefix>: the products sit on independent native version
# lines (RTC 4.6.x, RTM 2.2.x), so no single version can be stamped across them — which is why
# there is no way to pass one.
#
# Packages are written to ./artifacts.
#
# Each .NET SDK's workloads support only two target frameworks per platform (the .NET 9 band
# builds net8/net9, the .NET 10 band builds net10), so this runs two passes and merges them with
# build/merge-packages.py. global.json pins the .NET 9 SDK, and the SDK is resolved from the
# working directory, so the second pass runs from a scratch directory carrying its own global.json.
#
# This can only run on macOS: Net.Agora.Video targets iOS target frameworks too, which needs the
# iOS workload to restore even when nothing here compiles Objective-C.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
. "${SCRIPT_DIR}/pins.sh"

PRODUCT="${1:-}"
if [ -z "${PRODUCT}" ]; then
    echo "usage: $0 <product> [--suffix <prerelease>]" >&2
    echo "  known products: video, voice, signaling" >&2
    exit 2
fi

SUFFIX=""
case "${2:-}" in
    "") ;;
    --suffix)
        SUFFIX="${3:?--suffix needs a value}"
        ;;
    *)
        echo "error: unknown argument '$2' (a single version cannot be stamped across" >&2
        echo "       independent version lines — use --suffix for prereleases)" >&2
        exit 2
        ;;
esac

case "${PRODUCT}" in
    video)
        NAME="Video"
        ;;
    voice)
        NAME="Voice"
        ;;
    signaling)
        NAME="Signaling"
        ;;
    *)
        echo "error: unknown product '${PRODUCT}'" >&2
        exit 1
        ;;
esac

ROOT="${AGORA_REPO_ROOT}"
OUTPUT="${ROOT}/artifacts"

PASS1_BAND="net9"
PASS2_BAND="net10"
PASS2_SDK="10.0.100"

VERSION_ARG=""
if [ -n "${SUFFIX}" ]; then
    case "${SUFFIX}" in
        *[!A-Za-z0-9.-]*)
            echo "error: invalid suffix '${SUFFIX}'" >&2
            exit 1
            ;;
    esac
    VERSION_ARG="-p:VersionSuffix=${SUFFIX}"
fi

# Scratch directories for the two passes, deliberately *outside* artifacts/: NuGet folder sources
# search subdirectories, so a pass directory under artifacts/ would let the metapackage restore
# resolve an unmerged single-target-framework package and fail with NU1202.
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

PASS1_DIR="${WORK}/net9-pass"
PASS2_DIR="${WORK}/net10-pass"

SDK10_DIR="${WORK}/sdk10"
mkdir -p "${SDK10_DIR}"
cat > "${SDK10_DIR}/global.json" <<EOF
{ "sdk": { "version": "${PASS2_SDK}", "rollForward": "latestFeature" } }
EOF

# Packs one project in both SDK bands, then merges the two packages into artifacts/.
pack_and_merge() {
    local project="$1" name directory
    name="$(basename "${project}" .csproj)"
    directory="$(dirname "${project}")"

    rm -rf "${directory}/obj" "${directory}/bin"

    echo "==> packing ${name} (${PASS1_BAND} band)"
    dotnet pack "${project}" \
        -c Release \
        -p:AgoraSdkBand="${PASS1_BAND}" \
        ${VERSION_ARG} \
        -o "${PASS1_DIR}"

    echo "==> packing ${name} (${PASS2_BAND} band)"
    ( cd "${SDK10_DIR}" && dotnet pack "${project}" \
        -c Release \
        -p:AgoraSdkBand="${PASS2_BAND}" \
        ${VERSION_ARG} \
        -o "${PASS2_DIR}" )

    echo "==> merging ${name}"
    python3 "${SCRIPT_DIR}/merge-packages.py" "${PASS1_DIR}" "${PASS2_DIR}" "${OUTPUT}"

    rm -rf "${PASS1_DIR}" "${PASS2_DIR}"
}

# Order matters: Net.Agora.<Name>.Maui depends on Net.Agora.<Name>, which must already be in
# artifacts/ for its restore to resolve the just-packed version rather than a stale or published
# one — see the PackageReference version pins in each .csproj.
pack_and_merge "${ROOT}/src/Net.Agora.${NAME}/Net.Agora.${NAME}.csproj"
if [ -f "${ROOT}/src/Net.Agora.${NAME}.Maui/Net.Agora.${NAME}.Maui.csproj" ]; then
    pack_and_merge "${ROOT}/src/Net.Agora.${NAME}.Maui/Net.Agora.${NAME}.Maui.csproj"
fi

echo "==> packages in ${OUTPUT}:"
ls -1 "${OUTPUT}"/*.nupkg
