#!/usr/bin/env bash
set -euo pipefail

# Packs the Net.Agora façade packages for a product — the cross-platform client and, where one
# exists, its MAUI package. Does NOT build any raw platform binding: those are published from
# sbokatuk/Net.Agora.Android and sbokatuk/Net.Agora.iOS, and must already be resolvable — either
# from nuget.org, or from this repository's ./artifacts (NuGet.config's local-artifacts source),
# which is where you'd copy their own build/BuildNugets.sh output for local testing.
#
# Usage:
#   ./build/BuildNugets.sh video                         # one product, its own version from Directory.Build.props
#   ./build/BuildNugets.sh voice --suffix beta.12.34     # same, with a prerelease suffix appended
#   ./build/BuildNugets.sh --track rtc                   # every product on a release track (rtc = video + voice)
#   ./build/BuildNugets.sh --track chat --suffix beta.1  # both
#
# A product is its cross-platform package and, where one exists, its .Maui companion. --track packs
# every product on one release track (see build/tracks.tsv), which is how a release publishes a
# whole track from one tag; a bare product name packs just that one.
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

PRODUCTS=""
TRACK=""
SUFFIX=""
while [ $# -gt 0 ]; do
    case "$1" in
        --track)
            TRACK="${2:?--track needs a value}"
            shift 2
            ;;
        --suffix)
            SUFFIX="${2:?--suffix needs a value}"
            shift 2
            ;;
        -*)
            echo "error: unknown option '$1'" >&2
            exit 2
            ;;
        *)
            PRODUCTS="${PRODUCTS} $1"
            shift
            ;;
    esac
done

if [ -n "${TRACK}" ]; then
    if [ -n "${PRODUCTS# }" ]; then
        echo "error: pass a product name or --track, not both" >&2
        exit 2
    fi
    PRODUCTS="$(awk -F'\t' -v t="${TRACK}" '$1 == t { print $3 }' "${SCRIPT_DIR}/tracks.tsv")"
    if [ -z "${PRODUCTS}" ]; then
        echo "error: unknown track '${TRACK}' (not in build/tracks.tsv)" >&2
        exit 2
    fi
    echo "==> track '${TRACK}':${PRODUCTS# }" >&2
fi

if [ -z "${PRODUCTS# }" ]; then
    echo "usage: $0 <product> [--suffix <prerelease>]   |   $0 --track <track> [--suffix ...]" >&2
    echo "  known products: video, voice, signaling, chat, whiteboard, fastboard" >&2
    echo "  known tracks:   rtc, signaling, chat, whiteboard, fastboard (see build/tracks.tsv)" >&2
    exit 2
fi

# Maps a product name to the PascalCase segment its projects use.
product_name() {
    case "$1" in
        video)      echo Video ;;
        voice)      echo Voice ;;
        signaling)  echo Signaling ;;
        chat)       echo Chat ;;
        whiteboard) echo Whiteboard ;;
        fastboard)  echo Fastboard ;;
        *)          echo "error: unknown product '$1'" >&2; return 1 ;;
    esac
}

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

# One or more products (a --track expands to several). For each, order matters:
# Net.Agora.<Name>.Maui depends on Net.Agora.<Name>, which must already be in artifacts/ for its
# restore to resolve the just-packed version rather than a stale or published one — see the
# PackageReference version pins in each .csproj.
for product in ${PRODUCTS}; do
    NAME="$(product_name "${product}")"

    pack_and_merge "${ROOT}/src/Net.Agora.${NAME}/Net.Agora.${NAME}.csproj"
    if [ -f "${ROOT}/src/Net.Agora.${NAME}.Maui/Net.Agora.${NAME}.Maui.csproj" ]; then
        pack_and_merge "${ROOT}/src/Net.Agora.${NAME}.Maui/Net.Agora.${NAME}.Maui.csproj"
    fi
done

echo "==> packages in ${OUTPUT}:"
ls -1 "${OUTPUT}"/*.nupkg
