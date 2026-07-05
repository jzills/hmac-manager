# GitHub Releases for the Four Release Pipelines — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make each of the four release pipelines (NuGet, Kubernetes Service, Kubernetes Operator, Helm Chart) create a GitHub Release when its artifact ships, so releases are visible on the repo.

**Architecture:** Two small, focused shell scripts under `.github/scripts/` (mirroring the existing `extract-version.sh` convention): `previous-tag.sh` computes the prior tag *within the same prefix stream* (pure git, unit-testable), and `create-release.sh` orchestrates the release (idempotency check → generate changelog → compose notes → `gh release create`). Each pipeline's `publish` job gains one final step that calls `create-release.sh` after the artifact has been published.

**Tech Stack:** Bash, `gh` CLI (GitHub Actions preinstalls it), `jq`, GitHub Actions workflow YAML.

## Global Constraints

- Scripts are `bash` with `set -euo pipefail`; resolve their own directory via `${BASH_SOURCE[0]}`.
- Workflows invoke scripts as `bash .github/scripts/<name>.sh ...` (matches the existing `tag.yml` → `extract-version.sh` convention; no reliance on the execute bit).
- Release bodies use **inline code** (single backticks), never fenced ``` blocks — this avoids fragile triple-backtick nesting inside a heredoc inside YAML.
- All interpolated values in release bodies are **Actions expressions** (`${{ ... }}`), never shell `${VAR}` — Actions substitutes them before the runner writes the step script, so they resolve even inside a single-quoted `<<'BODY'` heredoc.
- Release titles, verbatim: `HmacManager (NuGet) vX.Y.Z`, `HmacManager (Kubernetes Service) vX.Y.Z`, `HmacManager (Kubernetes Operator) vX.Y.Z`, `HmacManager (Helm Chart) vX.Y.Z` (the `X.Y.Z` comes from `${{ steps.version.outputs.value }}`).
- Every release: `--latest` (option B — newest published wins the badge), notes-only (no attached assets), created with the built-in `GITHUB_TOKEN` via `env: GH_TOKEN`, job `permissions: contents: write`, checkout `fetch-depth: 0`.
- Releases are **idempotent**: if the release already exists, skip without error (re-run safe, like `--skip-duplicate` on the NuGet push).
- The release step is always the **last** step of the `publish` job, so it only runs after a successful artifact publish.
- Work happens on branch `feat/github-releases-for-pipelines` (already created). Commit after each task.

---

### Task 1: `previous-tag.sh` — same-prefix previous tag

**Files:**
- Create: `.github/scripts/previous-tag.sh`
- Test: `.github/scripts/previous-tag.test.sh`

**Interfaces:**
- Consumes: nothing (leaf).
- Produces: `previous-tag.sh <TAG>` prints the release tag immediately preceding `<TAG>` within the same prefix stream (e.g. the prior `nuget/v*` for a `nuget/v*` tag), or an empty string if `<TAG>` is the first release of its prefix. Reads tags from the git repo in the current working directory.

- [ ] **Step 1: Write the failing test**

Create `.github/scripts/previous-tag.test.sh`:

```bash
#!/usr/bin/env bash
# Unit tests for previous-tag.sh — pure git, no network, no gh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNDER_TEST="$SCRIPT_DIR/previous-tag.sh"

fail=0
assert_eq() { # desc expected actual
  if [ "$2" = "$3" ]; then
    echo "ok   - $1"
  else
    echo "FAIL - $1 (expected='$2' actual='$3')"
    fail=1
  fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
git -C "$TMP" init -q
git -C "$TMP" config user.email t@t.t
git -C "$TMP" config user.name t
git -C "$TMP" commit -q --allow-empty -m init
for tag in nuget/v2.6.0 nuget/v2.7.0 nuget/v2.10.0 service/v1.2.0 chart/v0.2.0 chart/v0.3.0; do
  git -C "$TMP" tag "$tag"
done

run() { ( cd "$TMP" && "$UNDER_TEST" "$1" ); }

assert_eq "nuget/v2.10.0 -> prior nuget by semver (2.10 > 2.7)" "nuget/v2.7.0" "$(run nuget/v2.10.0)"
assert_eq "nuget/v2.7.0 -> prior nuget"                          "nuget/v2.6.0" "$(run nuget/v2.7.0)"
assert_eq "chart/v0.3.0 -> prior chart, not cross-stream"        "chart/v0.2.0" "$(run chart/v0.3.0)"
assert_eq "nuget/v2.6.0 is oldest nuget -> empty"                ""             "$(run nuget/v2.6.0)"
assert_eq "service/v1.2.0 is only service release -> empty"      ""             "$(run service/v1.2.0)"

exit $fail
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `bash .github/scripts/previous-tag.test.sh`
Expected: FAIL — the script does not exist yet, so `$UNDER_TEST` errors ("No such file or directory").

- [ ] **Step 3: Write the minimal implementation**

Create `.github/scripts/previous-tag.sh`:

```bash
#!/usr/bin/env bash
# previous-tag.sh <TAG>
#
# Prints the release tag immediately preceding <TAG> within the SAME prefix
# stream (e.g. the prior `nuget/v*` for a `nuget/v*` tag), or an empty string
# when <TAG> is the first release of its prefix. Used to scope auto-generated
# release changelogs so one artifact's notes are not polluted by another's.
#
# Requires the repository's tags to be present locally (checkout fetch-depth: 0).
set -euo pipefail

TAG="${1:?usage: previous-tag.sh <TAG>}"

# Prefix = the stream name before "/v" (e.g. "nuget/v2.7.0" -> "nuget").
PREFIX="${TAG%/v*}"

prev=""
seen_current=0
while IFS= read -r t; do
  [ -z "$t" ] && continue
  if [ "$seen_current" -eq 1 ]; then
    prev="$t"
    break
  fi
  [ "$t" = "$TAG" ] && seen_current=1
done < <(git tag --list "${PREFIX}/v*" --sort=-v:refname)

printf '%s\n' "$prev"
```

- [ ] **Step 4: Run the test and confirm it passes**

Run: `bash .github/scripts/previous-tag.test.sh`
Expected: all 5 lines `ok - ...`, exit 0.

- [ ] **Step 5: Lint**

Run: `shellcheck .github/scripts/previous-tag.sh .github/scripts/previous-tag.test.sh`
Expected: no output (clean).

- [ ] **Step 6: Commit**

```bash
chmod +x .github/scripts/previous-tag.sh .github/scripts/previous-tag.test.sh
git add .github/scripts/previous-tag.sh .github/scripts/previous-tag.test.sh
git commit -m "feat(ci): add previous-tag.sh for same-prefix changelog scoping"
```

---

### Task 2: `create-release.sh` — orchestrate the GitHub Release

**Files:**
- Create: `.github/scripts/create-release.sh`
- Test: `.github/scripts/create-release.test.sh`

**Interfaces:**
- Consumes: `previous-tag.sh <TAG>` (Task 1); env `GITHUB_REPOSITORY` and `GH_TOKEN`; `gh` and `jq` on PATH.
- Produces: `create-release.sh <TAG> <TITLE>` — reads install-info markdown from stdin, and if no release exists for `<TAG>`, creates one titled `<TITLE>`, marked `--latest`, with notes = install body + auto-generated same-prefix changelog. If the release already exists, prints a skip message and exits 0.

- [ ] **Step 1: Write the failing test**

Create `.github/scripts/create-release.test.sh`:

```bash
#!/usr/bin/env bash
# Tests create-release.sh with a stubbed `gh` — no network.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail=0
assert_contains() { # desc haystack needle
  if printf '%s' "$2" | grep -qF -- "$3"; then
    echo "ok   - $1"
  else
    echo "FAIL - $1 (missing: $3)"
    fail=1
  fi
}
assert_absent() { # desc path
  if [ ! -e "$2" ]; then echo "ok   - $1"; else echo "FAIL - $1 (exists: $2)"; fail=1; fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Throwaway repo so previous-tag.sh has real tags to read.
REPO="$TMP/repo"
mkdir -p "$REPO/.github/scripts"
git -C "$REPO" init -q
git -C "$REPO" config user.email t@t.t
git -C "$REPO" config user.name t
git -C "$REPO" commit -q --allow-empty -m init
git -C "$REPO" tag nuget/v2.6.0
git -C "$REPO" tag nuget/v2.7.0
cp "$SCRIPT_DIR/create-release.sh" "$SCRIPT_DIR/previous-tag.sh" "$REPO/.github/scripts/"

# Fake gh: records `release create` args + piped notes; toggles existence.
BIN="$TMP/bin"; mkdir -p "$BIN"
cat > "$BIN/gh" <<'GH'
#!/usr/bin/env bash
set -euo pipefail
case "$1 $2" in
  "release view")
    [ "${GH_TEST_RELEASE_EXISTS:-0}" = "1" ] && exit 0 || exit 1 ;;
  "api "*|"api")
    echo '{"body":"## Changes\n* thing by @x in #1\n\n**Full Changelog**: nuget/v2.6.0...nuget/v2.7.0"}' ;;
  "release create")
    printf '%s\n' "$*" > "$GH_TEST_LOG_DIR/create_args"
    cat > "$GH_TEST_LOG_DIR/create_notes" ;;
  *) echo "unexpected gh call: $*" >&2; exit 99 ;;
esac
GH
chmod +x "$BIN/gh"

export PATH="$BIN:$PATH"
export GITHUB_REPOSITORY="jzills/hmac-manager"
export GH_TEST_LOG_DIR="$TMP/log"; mkdir -p "$GH_TEST_LOG_DIR"

# --- Case 1: no existing release -> creates with correct args + notes ---
rm -f "$GH_TEST_LOG_DIR/create_args" "$GH_TEST_LOG_DIR/create_notes"
( cd "$REPO" && GH_TEST_RELEASE_EXISTS=0 \
    bash .github/scripts/create-release.sh "nuget/v2.7.0" "HmacManager (NuGet) v2.7.0" <<'BODY'
**HmacManager (NuGet)** — core library.
Install: `dotnet add package HmacManager --version 2.7.0`
BODY
)
args="$(cat "$GH_TEST_LOG_DIR/create_args" 2>/dev/null || true)"
notes="$(cat "$GH_TEST_LOG_DIR/create_notes" 2>/dev/null || true)"
assert_contains "creates the release tag"    "$args"  "nuget/v2.7.0"
assert_contains "passes the title"           "$args"  "HmacManager (NuGet) v2.7.0"
assert_contains "marks as latest"            "$args"  "--latest"
assert_contains "verifies the tag"           "$args"  "--verify-tag"
assert_contains "notes carry install body"   "$notes" "dotnet add package HmacManager --version 2.7.0"
assert_contains "notes carry the changelog"  "$notes" "Full Changelog"

# --- Case 2: release already exists -> skips, no create call ---
rm -f "$GH_TEST_LOG_DIR/create_args" "$GH_TEST_LOG_DIR/create_notes"
( cd "$REPO" && GH_TEST_RELEASE_EXISTS=1 \
    bash .github/scripts/create-release.sh "nuget/v2.7.0" "HmacManager (NuGet) v2.7.0" <<'BODY'
whatever
BODY
)
assert_absent "existing release untouched (no create call)" "$GH_TEST_LOG_DIR/create_args"

exit $fail
```

- [ ] **Step 2: Run the test and confirm it fails**

Run: `bash .github/scripts/create-release.test.sh`
Expected: FAIL — `cp` of the not-yet-created `create-release.sh` errors out.

- [ ] **Step 3: Write the minimal implementation**

Create `.github/scripts/create-release.sh`:

```bash
#!/usr/bin/env bash
# create-release.sh <TAG> <TITLE>
#
# Creates a GitHub Release for an already-pushed tag. Install-info markdown is
# read from stdin; a changelog scoped to the previous same-prefix tag is
# appended automatically. Idempotent: if the release already exists it is left
# untouched.
#
# Requires: gh authenticated via GH_TOKEN; env GITHUB_REPOSITORY; job permission
# `contents: write`; checkout `fetch-depth: 0` (so previous-tag.sh sees tags).
# jq is preinstalled on GitHub-hosted runners.
set -euo pipefail

TAG="${1:?usage: create-release.sh <TAG> <TITLE>}"
TITLE="${2:?usage: create-release.sh <TAG> <TITLE>}"
install_body="$(cat)"

if gh release view "$TAG" >/dev/null 2>&1; then
  echo "Release '$TAG' already exists — skipping."
  exit 0
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
prev_tag="$("$script_dir/previous-tag.sh" "$TAG")"

gen_args=(--method POST "repos/${GITHUB_REPOSITORY}/releases/generate-notes" -f "tag_name=${TAG}")
if [ -n "$prev_tag" ]; then
  gen_args+=(-f "previous_tag_name=${prev_tag}")
fi
changelog="$(gh api "${gen_args[@]}" | jq -r '.body')"

notes="$(printf '%s\n\n%s\n' "$install_body" "$changelog")"

printf '%s' "$notes" | gh release create "$TAG" \
  --title "$TITLE" \
  --notes-file - \
  --latest \
  --verify-tag

echo "Created release '$TAG'."
```

- [ ] **Step 4: Run the test and confirm it passes**

Run: `bash .github/scripts/create-release.test.sh`
Expected: all 7 lines `ok - ...`, exit 0.

- [ ] **Step 5: Lint**

Run: `shellcheck .github/scripts/create-release.sh .github/scripts/create-release.test.sh`
Expected: no output (clean).

- [ ] **Step 6: Commit**

```bash
chmod +x .github/scripts/create-release.sh .github/scripts/create-release.test.sh
git add .github/scripts/create-release.sh .github/scripts/create-release.test.sh
git commit -m "feat(ci): add create-release.sh to publish GitHub Releases"
```

---

### Task 3: Wire the release step into all four pipelines

**Files:**
- Modify: `.github/workflows/release.yml`
- Modify: `.github/workflows/service-release.yml`
- Modify: `.github/workflows/operator-release.yml`
- Modify: `.github/workflows/chart-release.yml`

**Interfaces:**
- Consumes: `create-release.sh <TAG> <TITLE>` (Task 2). Each step passes `$GITHUB_REF_NAME` (the tag) and the artifact title; the install body is piped via a single-quoted heredoc.
- Produces: no code interface; four workflows that each cut a GitHub Release on their tag.

- [ ] **Step 1: `release.yml` — add permission + release step**

The `publish` job already checks out with `fetch-depth: 0`. Add the `permissions` block: change

```yaml
  publish:
    name: Pack and Publish
    runs-on: ubuntu-latest
    needs: test
    steps:
```

to

```yaml
  publish:
    name: Pack and Publish
    runs-on: ubuntu-latest
    needs: test
    permissions:
      contents: write
    steps:
```

Then append this as the final step of the `publish` job (after the `Push to NuGet` step):

```yaml
      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          bash .github/scripts/create-release.sh "$GITHUB_REF_NAME" "HmacManager (NuGet) v${{ steps.version.outputs.value }}" <<'BODY'
          **HmacManager (NuGet)** — core ASP.NET Core HMAC authentication library.

          Install: `dotnet add package HmacManager --version ${{ steps.version.outputs.value }}`

          📦 https://www.nuget.org/packages/HmacManager/${{ steps.version.outputs.value }}
          BODY
```

- [ ] **Step 2: `service-release.yml` — add fetch-depth, permission + release step**

Change the `publish` job header to add `permissions`, and the checkout to add `fetch-depth: 0`:

```yaml
  publish:
    name: Build and Push Docker Image
    runs-on: ubuntu-latest
    needs: test
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
```

Append this as the final step of the `publish` job (after the `Update Docker Hub description` step):

```yaml
      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          bash .github/scripts/create-release.sh "$GITHUB_REF_NAME" "HmacManager (Kubernetes Service) v${{ steps.version.outputs.value }}" <<'BODY'
          **HmacManager (Kubernetes Service)** — ext-authz HMAC verification service image.

          Pull: `docker pull ${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager:${{ steps.version.outputs.value }}`

          🐳 https://hub.docker.com/r/${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager
          BODY
```

- [ ] **Step 3: `operator-release.yml` — add fetch-depth, permission + release step**

Change the `publish` job header + checkout the same way:

```yaml
  publish:
    name: Build and Push Docker Image
    runs-on: ubuntu-latest
    needs: test
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
```

Append this as the final step of the `publish` job (after the `Update Docker Hub description` step):

```yaml
      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          bash .github/scripts/create-release.sh "$GITHUB_REF_NAME" "HmacManager (Kubernetes Operator) v${{ steps.version.outputs.value }}" <<'BODY'
          **HmacManager (Kubernetes Operator)** — HmacPolicy CRD controller image.

          Pull: `docker pull ${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager-operator:${{ steps.version.outputs.value }}`

          🐳 https://hub.docker.com/r/${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager-operator
          BODY
```

- [ ] **Step 4: `chart-release.yml` — widen permission, add fetch-depth + release step**

In the `publish` job, change `contents: read` to `contents: write` (keep `packages: write`) and add `fetch-depth: 0` to its checkout:

```yaml
  publish:
    name: Package and Push Helm Chart
    runs-on: ubuntu-latest
    permissions:
      contents: write
      packages: write
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
```

Append this as the final step of the `publish` job (after the `Push chart to GHCR` step):

```yaml
      # Notes-only GitHub Release (no .tgz asset): the chart is served via GHCR
      # (OCI) and the gh-pages HTTP repo, and immutable releases also reject a
      # post-create asset upload. The release exists purely for repo visibility.
      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          bash .github/scripts/create-release.sh "$GITHUB_REF_NAME" "HmacManager (Helm Chart) v${{ steps.version.outputs.value }}" <<'BODY'
          **HmacManager (Helm Chart)** — Helm chart for deploying HmacManager on Kubernetes.

          Install: `helm install hmac-manager oci://ghcr.io/${{ github.repository_owner }}/charts/hmac-manager --version ${{ steps.version.outputs.value }}`

          Also available from the HTTP repo: https://${{ github.repository_owner }}.github.io/${{ github.event.repository.name }}
          BODY
```

- [ ] **Step 5: Validate all four workflows parse as YAML**

Run:

```bash
python3 - <<'PY'
import yaml, sys
for f in ["release", "service-release", "operator-release", "chart-release"]:
    p = f".github/workflows/{f}.yml"
    with open(p) as fh:
        yaml.safe_load(fh)
    print("ok", p)
PY
```

Expected: four `ok .github/workflows/*.yml` lines, no traceback. (A `yaml.scanner.ScannerError` here almost always means a heredoc line was over-indented — the `<<'BODY'` body and the closing `BODY` must sit at the same indentation as the `bash .github/...` line so the YAML block scalar strips them to column 0.)

- [ ] **Step 6 (optional): actionlint via Docker**

If network is available: `docker run --rm -v "$(pwd):/repo" -w /repo rhysd/actionlint:latest -color`
Expected: no findings for the four edited workflows. Skip if offline — Step 5 plus the shellcheck from Tasks 1–2 already cover YAML and script correctness; GitHub validates the workflow on push regardless.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/release.yml .github/workflows/service-release.yml \
        .github/workflows/operator-release.yml .github/workflows/chart-release.yml
git commit -m "feat(ci): create a GitHub Release from each artifact pipeline"
```

---

### Task 4: Document the new release step

**Files:**
- Modify: `RELEASING.md`
- Modify: `CLAUDE.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: RELEASING.md — note the GitHub Release**

Immediately after the "Artifacts and Tag Prefixes" table (before the paragraph beginning "The published version always comes from the tag"), insert:

```markdown
Each pipeline also creates a **GitHub Release** for its tag (via
`.github/scripts/create-release.sh`): titled `HmacManager (<Kind>) vX.Y.Z`,
marked as the latest release, with install instructions and a changelog
auto-generated from the merged PRs since the previous release of the *same*
artifact. Releases are notes-only — the artifacts themselves live on nuget.org,
Docker Hub, and GHCR / the gh-pages HTTP repo.
```

- [ ] **Step 2: CLAUDE.md — extend the release-pipeline intro**

In the "CI/CD Pipelines" section, find the sentence ending "...documented in [RELEASING.md](RELEASING.md)." and append to that paragraph:

```markdown
 Each of these pipelines also creates a GitHub Release for its tag via `.github/scripts/create-release.sh` (uniform `HmacManager (<Kind>)` titles, marked latest, with install info and an auto-generated changelog scoped to the previous same-prefix tag).
```

- [ ] **Step 3: CLAUDE.md — add the step to release.yml's `publish` job list**

In the `release.yml` → `publish` job numbered list, after item 5 ("Pushes the `.nupkg` to NuGet Gallery..."), add:

```markdown
6. Creates a GitHub Release for the tag via `.github/scripts/create-release.sh` — titled `HmacManager (NuGet) vX.Y.Z`, marked as latest, with install instructions and a changelog auto-generated since the previous `nuget/v*` tag.
```

- [ ] **Step 4: Verify docs render / links are intact**

Run: `grep -n "create-release.sh" RELEASING.md CLAUDE.md`
Expected: at least one match in each file.

- [ ] **Step 5: Commit**

```bash
git add RELEASING.md CLAUDE.md
git commit -m "docs: document the GitHub Release step in the release pipelines"
```

---

## Final Verification (after all tasks)

- [ ] Run both script test suites: `bash .github/scripts/previous-tag.test.sh && bash .github/scripts/create-release.test.sh` → all `ok`.
- [ ] `shellcheck .github/scripts/*.sh` → clean.
- [ ] All four workflows parse (Task 3 Step 5) → four `ok`.
- [ ] **End-to-end (real signal, done on the next real release or a disposable test tag):** cut a release and confirm on `/releases` that the release appears with the correct `HmacManager (<Kind>)` title, install body, generated changelog, and the "Latest" badge — and that re-running the workflow is a no-op (idempotency skip).

## Notes / Out of Scope

- The script tests run **locally / during implementation**, not in `pr.yml`. Wiring a shell-test job into `pr.yml` would prevent rot but is out of scope here; consider it a follow-up.
- No waiting for nuget.org / registry indexing before cutting the release (the release may exist a minute before the package is installable — cosmetic, matches current behavior).
- No attached release assets; no combined multi-artifact release.
