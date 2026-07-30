---
applyTo: ".github/workflows/*.yml"
description: Workflow contract for the Net.Agora pipeline.
---

# Workflow conventions

- `build.yml` is the reusable pipeline; `pr.yml` and `release.yml` are its only callers. Keep its
  inputs' names and meanings identical to the rest of the Net.Agora family:
  - `suffix` — prerelease suffix appended to each product's own version; each product still packs
    at its own pin, so never introduce a single repository-wide version input.
  - `verify` — gates unit tests, package validation, sample builds and the e2e matrices. Pull
    requests leave it `true`; releases pass `false` because the tagged commit was verified on its
    pull request. Every verification job carries `if: ${{ inputs.verify }}`.
  - `track` — scopes *what* is packed to one row of `build/tracks.tsv`; empty packs everything. It
    is orthogonal to `verify`.
- Call `./build/BuildNugets.sh` and `build/pins.sh` rather than inlining `dotnet pack` or grepping
  `Directory.Build.props`. Keep `run:` steps that source `pins.sh` on `bash` (it is not POSIX sh).
- Pack on `macos-15`, pinned rather than `macos-latest`, and run `./.github/scripts/select-xcode.sh`
  before any Xcode step. Install workloads per SDK band with a scratch `global.json`, since the
  net10 band needs its own SDK.
- Keep the `NuGet/login@v1` OIDC step immediately before `dotnet nuget push`: the issued key lasts
  one hour and each token can be exchanged exactly once. Publishing jobs need
  `id-token: write`, `environment: nuget.org`, and their own nuget.org trusted-publishing policy —
  policies are per workflow file, so `pr.yml`'s does not cover `release.yml`. `NUGET_USER` is the
  only secret; never add a long-lived API key.
- Keep `pr.yml`'s publish job gated on
  `github.event.pull_request.head.repo.full_name == github.repository` — fork pull requests get no
  OIDC token and must still build and test.
- Push with `--skip-duplicate`, so re-runs and tracks whose pins did not move are no-ops.
- Keep `release.yml`'s `guard` job (tag must be an ancestor of the default branch) and its
  `resolve` job (`./build/resolve-track.sh`) ahead of `build`; both are what make `verify: false`
  safe.
- Keep `auto-release.yml` triggered on pushes to `main` under `docs/release-notes/**`, matching
  added files only (`--diff-filter=A`), and dispatching `release.yml` at the new tag — a tag pushed
  with `GITHUB_TOKEN` does not fire `on: push: tags`.
- Grant the narrowest `permissions:` per job, and keep a `concurrency:` group on every workflow.
