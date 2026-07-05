# Design: GitHub Releases for the four release pipelines

**Date:** 2026-07-05
**Status:** Approved (pending spec review)

## Problem

The repository has four independently versioned artifacts, each with its own
release pipeline driven by a prefixed tag:

| Artifact | Tag | Pipeline | Publishes to |
|---|---|---|---|
| NuGet package | `nuget/vX.Y.Z` | `release.yml` | nuget.org |
| ext-authz service image | `service/vX.Y.Z` | `service-release.yml` | Docker Hub |
| policy operator image | `operator/vX.Y.Z` | `operator-release.yml` | Docker Hub |
| Helm chart | `chart/vX.Y.Z` | `chart-release.yml` | GHCR (OCI) + gh-pages HTTP repo |

All four **publish their artifact** and **push a git tag**, but none of them ever
create a **GitHub Release** object. Pushing a git tag only creates a git ref
(visible under `/tags`); a GitHub Release is a separate object created only by an
explicit call to the Releases API. Confirmed: `grep -rniE "gh release create|action-gh-release|actions/create-release|chart-releaser|releases/create"`
over `.github/` returns nothing. Result: the repo's `/releases` page is empty
even though artifacts are shipping.

The chart pipeline additionally has **immutable releases** enabled on the repo
(see the comment in `chart-release.yml`), which rejects attaching assets to a
release *after* it is created — this is why the chart tarball is served from
`gh-pages` rather than as a release asset.

## Goal

Each of the four pipelines should create a GitHub Release when its artifact
ships, so releases are visible on the repo. The Releases page will then list all
four independently versioned streams.

## Decisions (agreed during brainstorming)

1. **Four independent releases**, one per artifact tag stream — not a coordinated
   set. Each `release/*` branch merge produces exactly one prefixed tag
   (`extract-version.sh`), so each release event maps to one artifact.
2. **Notes content = install info + auto-generated changelog.** The changelog is
   generated against the **previous tag of the same prefix** so it is not
   polluted by other artifacts' releases.
3. **Chart release is notes-only** (no attached `.tgz`). The chart is already
   served via GHCR (OCI) and the gh-pages HTTP repo, and that repo's `index.yaml`
   points at gh-pages URLs — not release assets — so an attached tarball would be
   an unreferenced third copy. Notes-only also sidesteps the immutable-releases
   asset constraint entirely.
4. **`--latest=true` on all four** ("Option B"): the most recently *published*
   release always wears the "Latest" badge and headlines the homepage sidebar
   widget, regardless of artifact. Release titles name their artifact, so there is
   no ambiguity about what "Latest" refers to. (Note: `--latest=false` on all
   would freeze the badge and let it go stale; the default would let the
   highest-semver NuGet release permanently hold it. B is "newest wins.")
5. **Release runs only after a successful publish.** The release step is the final
   step of each `publish` job, so it runs only if every prior step (including the
   artifact push) succeeded. Jobs already gate on `needs: test` where a test job
   exists.

Out of scope (YAGNI): waiting for nuget.org/registry indexing to complete before
cutting the release; attaching artifacts as release assets; a combined
multi-artifact release.

## Design

### Shared helper: `.github/scripts/create-release.sh`

A single script (mirrors the existing `.github/scripts/extract-version.sh`
convention) so the release logic is not duplicated across four workflows.

**Interface:**

```
create-release.sh <TAG> <TITLE>
# install-info markdown is read from stdin (heredoc)
# requires env: GH_TOKEN
```

**Behavior:**

1. Derive `PREFIX` from `TAG` (everything before `/v`, e.g. `nuget/v2.7.0` → `nuget`).
2. Compute the **previous same-prefix tag**:
   `git tag --list "$PREFIX/v*" --sort=-v:refname`, then select the entry
   immediately after `$TAG` in the sorted list. Empty ⇒ this is the first release
   of that artifact.
3. Generate the changelog via the Releases API:
   `gh api --method POST repos/$GITHUB_REPOSITORY/releases/generate-notes
   -f tag_name="$TAG" [-f previous_tag_name="$PREV"]`, extract `.body` with `jq`.
   The API body includes a "What's Changed" section and a "Full Changelog"
   compare link.
4. Compose the final notes = install-info (from stdin) + a blank line + the
   generated changelog body. Composed locally rather than relying on `gh`'s
   `--notes`/`--generate-notes` concatenation semantics, so the output is
   deterministic.
5. **Idempotency:** if `gh release view "$TAG"` succeeds, the release already
   exists — log and exit 0 (safe re-runs, same spirit as `--skip-duplicate` on
   the NuGet push).
6. Create: `gh release create "$TAG" --title "$TITLE" --notes-file - --latest --verify-tag`
   (final notes piped on stdin). `--verify-tag` aborts if the tag is somehow
   missing from the remote.

**Caller requirements** (documented at the top of the script):
`GH_TOKEN` in env, job `permissions: contents: write`, and checkout with
`fetch-depth: 0` (needed so the previous-tag computation can see tags). `jq` is
preinstalled on `ubuntu-latest`.

### Per-pipeline wiring

Each pipeline gets one new final step in its `publish` job:

```yaml
- name: Create GitHub Release
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: |
    .github/scripts/create-release.sh "$GITHUB_REF_NAME" "HmacManager (<Kind>) v${{ steps.version.outputs.value }}" <<'BODY'
    <install-info markdown>
    BODY
```

Plus these supporting changes:

| Pipeline | Job | Supporting change |
|---|---|---|
| `release.yml` | `publish` | add `permissions:` block with `contents: write` (checkout already `fetch-depth: 0`) |
| `service-release.yml` | `publish` | add `permissions: contents: write`; add `fetch-depth: 0` to checkout |
| `operator-release.yml` | `publish` | add `permissions: contents: write`; add `fetch-depth: 0` to checkout |
| `chart-release.yml` | `publish` | change `contents: read` → `contents: write` (keep `packages: write`); add `fetch-depth: 0` to checkout |

### Titles and install bodies

| Artifact | Title | Install body (before the generated changelog) |
|---|---|---|
| NuGet | `HmacManager (NuGet) vX.Y.Z` | "Core ASP.NET Core HMAC authentication library." · `dotnet add package HmacManager --version X.Y.Z` · link to `nuget.org/packages/HmacManager/X.Y.Z` |
| Service | `HmacManager (Kubernetes Service) vX.Y.Z` | ext-authz HMAC verification service. · `docker pull ${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager:X.Y.Z` · Docker Hub link |
| Operator | `HmacManager (Kubernetes Operator) vX.Y.Z` | `HmacPolicy` CRD controller. · `docker pull ${{ secrets.DOCKERHUB_USERNAME }}/hmac-manager-operator:X.Y.Z` · Docker Hub link |
| Chart | `HmacManager (Helm Chart) vX.Y.Z` | `helm install hmac-manager oci://ghcr.io/${{ github.repository_owner }}/charts/hmac-manager --version X.Y.Z` · gh-pages HTTP repo link |

The uniform `HmacManager (<Kind>)` title scheme groups the four interleaved
streams visually on the Releases page and keeps the "Latest" badge (option B,
which hops between artifacts) unambiguous. The precise "ext-authz" term for the
service is carried in its install body rather than the title.

**Expansion note:** all interpolated values in the install bodies must be
**Actions expressions** (`${{ secrets.DOCKERHUB_USERNAME }}`,
`${{ github.repository_owner }}`, `${{ steps.version.outputs.value }}`), not
shell `${VAR}` syntax. Actions substitutes `${{ }}` before the runner writes the
step script, so they resolve correctly even inside a quoted `<<'BODY'` heredoc
(which is quoted to prevent the shell from touching anything else in the notes).

The service/operator bodies reference the existing `secrets.DOCKERHUB_USERNAME`
(the same value already used to tag their images). It is a public value; Actions
masks it in workflow logs but writes the real value into the release body.

### Token & safety

Use the built-in `GITHUB_TOKEN` (via `env: GH_TOKEN`), **not** `RELEASE_PAT`.
Creating a release from inside an already-running workflow needs only
`contents: write`. No workflow triggers on `release:` events, so there is no
recursion risk, and the "tags pushed with `GITHUB_TOKEN` don't trigger release
workflows" rule from `RELEASING.md` is irrelevant here (we are not pushing tags).

### chart-release.yml comment

Extend the existing immutable-releases comment to note that the GitHub Release
this pipeline now creates is intentionally **notes-only** (no attached asset),
consistent with the same reasoning.

## Verification

1. **Unit-test the previous-tag logic** of `create-release.sh` against a
   throwaway git repo seeded with fake `nuget/v*` and `chart/v*` tags — prove
   cross-stream selection is correct (a `chart/*` release diffs against the prior
   `chart/*` tag, not a `nuget/*` tag) and that the first release of a prefix
   returns an empty previous tag.
2. **Shellcheck** `create-release.sh`.
3. **actionlint** the four workflow files (if available).
4. **End-to-end**: on the next real release (or a disposable test tag), confirm
   the release appears with the correct title, install body, generated changelog,
   and "Latest" badge, and that a re-run of the workflow is a no-op.

## Docs

- Update `RELEASING.md`: note each pipeline now also creates a GitHub Release, and
  reference `create-release.sh`.
- Update `CLAUDE.md` pipeline descriptions to mention the release step.
