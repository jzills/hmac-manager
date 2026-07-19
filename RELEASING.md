# Releasing

This project has five independently versioned artifacts, each with its own release pipeline. All releases follow Gitflow: work lands on `develop` via feature branches, gets stabilized on a release branch, merges into `main` **via a pull request**, and is tagged automatically at that merge commit.

Tagging is automated by `.github/workflows/tag.yml`: it fires whenever a PR whose head branch starts with `release/` is merged into `main`, extracts the artifact and version from the branch name (`.github/scripts/extract-version.sh`), and pushes the matching prefixed tag. That tag push — and the follow-up back-merge PR it opens into `develop` — use the **`RELEASE_PAT`** secret, not the built-in `GITHUB_TOKEN`: tags pushed with `GITHUB_TOKEN` are deliberately blocked from triggering the per-artifact release workflows, and `GITHUB_TOKEN` cannot enable auto-merge (nor do its bot-authored PRs run checks without a manual approval). Merging into `main` with a direct `git push` (bypassing a PR) will **not** trigger a release — the merge must go through a PR for `tag.yml` to fire.

> **Prerequisite for `gh pr merge --auto`:** the examples below queue the merge with `--auto`, which requires the repository's **Allow auto-merge** setting to be enabled (Settings → General → Pull Requests) and at least one required status check on `main`. If auto-merge is disabled, `gh pr merge --auto` errors out — drop `--auto` and run `gh pr merge --merge` to merge immediately once checks are green.

## Artifacts and Tag Prefixes

| Artifact | Release branch | Tag format | Trigger |
|---|---|---|---|
| NuGet package | `release/vX.Y.Z` | `nuget/vX.Y.Z` | `.github/workflows/release.yml` |
| Docker image (ext-authz service) | `release/service/vX.Y.Z` | `service/vX.Y.Z` | `.github/workflows/service-release.yml` |
| Docker image (policy operator) | `release/operator/vX.Y.Z` | `operator/vX.Y.Z` | `.github/workflows/operator-release.yml` |
| Helm chart | `release/chart/vX.Y.Z` | `chart/vX.Y.Z` | `.github/workflows/chart-release.yml` |
| npm package (TypeScript client) | `release/npm/vX.Y.Z` | `npm/vX.Y.Z` | `.github/workflows/npm-release.yml` |

Each pipeline also creates a **GitHub Release** for its tag (via
`.github/scripts/create-release.sh`): titled `HmacManager (<Kind>) vX.Y.Z`,
marked as the latest release, with install instructions and a changelog
auto-generated from the merged PRs since the previous release of the *same*
artifact. Releases are notes-only — the artifacts themselves live on nuget.org,
npmjs.com, Docker Hub, and GHCR / the gh-pages HTTP repo.

The published version always comes from the tag, not from any source file. Even so, each release branch must bump its own version file (`HmacManager.csproj`'s `<Version>` for NuGet, `HmacManager.Kubernetes.csproj`'s `<Version>` for the service, `HmacManager.Operator.csproj`'s `<Version>` for the operator, `Chart.yaml`'s `version` for the chart — see [Helm chart release](#helm-chart) for `appVersion` — and `client/lib/package.json`'s `version` for npm) so that:

- local/manual builds report the right version, and
- the release branch has a real commit to bring back into `develop`. If a release branch has no changes vs `develop` at merge time, `tag.yml`'s `merge-back` job fails loudly with an error telling you to bump the version — it does not silently skip.

---

## NuGet Package

Covers changes to the core library under `src/`.

```bash
# 1. Cut a release branch from develop
git checkout develop && git pull origin develop
git checkout -b release/v2.7.0

# 2. Bump the version and stabilize (bug fixes, changelog, etc.)
#    Edit src/HmacManager.csproj: <Version>2.7.0</Version>
git commit -am "chore: bump version to 2.7.0"
git push origin release/v2.7.0

# 3. Open a PR release/v2.7.0 -> main and merge it once checks pass.
#    Merging the PR automatically:
#      - tags main as nuget/v2.7.0 (triggers release.yml: test, pack, publish)
#      - opens and auto-merges a PR to back-merge release/v2.7.0 into develop
gh pr create --base main --head release/v2.7.0 --title "release: nuget v2.7.0" --fill
gh pr merge --merge --auto

# 4. Once the back-merge PR has merged, delete the release branch
git push origin --delete release/v2.7.0
git branch -d release/v2.7.0
```

**Required secret:** `NUGET_API_KEY` (Settings → Secrets → Actions).

---

## Docker Image (ext-authz service) {#docker-image}

Covers changes to `kubernetes/service/`.

```bash
# 1. Cut a release branch from develop
git checkout develop && git pull origin develop
git checkout -b release/service/v1.2.0

# 2. Bump the version and stabilize
#    Edit kubernetes/service/HmacManager.Kubernetes.csproj: <Version>1.2.0</Version>
git commit -am "chore: bump service version to 1.2.0"
git push origin release/service/v1.2.0

# 3. Open a PR release/service/v1.2.0 -> main and merge it once checks pass.
#    Merging the PR automatically:
#      - tags main as service/v1.2.0 (triggers service-release.yml: test, build, push image)
#      - opens and auto-merges a PR to back-merge release/service/v1.2.0 into develop
gh pr create --base main --head release/service/v1.2.0 --title "release: service v1.2.0" --fill
gh pr merge --merge --auto

# 4. Once the back-merge PR has merged, delete the release branch
git push origin --delete release/service/v1.2.0
git branch -d release/service/v1.2.0
```

**Required secrets:** `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` (Settings → Secrets → Actions).

Builds multi-arch images (`linux/amd64`, `linux/arm64`) and pushes two tags:
- `your-username/hmac-manager:1.2.0`
- `your-username/hmac-manager:latest`

---

## Docker Image (policy operator) {#policy-operator}

Covers changes to `kubernetes/operator/` — the controller that reconciles `HmacPolicy` custom resources into the aggregate ConfigMap and Secret the verifier pods mount. Released independently of the ext-authz service image.

```bash
# 1. Cut a release branch from develop
git checkout develop && git pull origin develop
git checkout -b release/operator/v1.1.0

# 2. Bump the version and stabilize
#    Edit kubernetes/operator/HmacManager.Operator.csproj: <Version>1.1.0</Version>
git commit -am "chore: bump operator version to 1.1.0"
git push origin release/operator/v1.1.0

# 3. Open a PR release/operator/v1.1.0 -> main and merge it once checks pass.
#    Merging the PR automatically:
#      - tags main as operator/v1.1.0 (triggers operator-release.yml: test, build, push image)
#      - opens and auto-merges a PR to back-merge release/operator/v1.1.0 into develop
gh pr create --base main --head release/operator/v1.1.0 --title "release: operator v1.1.0" --fill
gh pr merge --merge --auto

# 4. Once the back-merge PR has merged, delete the release branch
git push origin --delete release/operator/v1.1.0
git branch -d release/operator/v1.1.0
```

**Required secrets:** `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` (Settings → Secrets → Actions).

Builds multi-arch images (`linux/amd64`, `linux/arm64`) and pushes two tags:
- `your-username/hmac-manager-operator:1.1.0`
- `your-username/hmac-manager-operator:latest`

The chart deploys this image via `operator.image.repository` / `operator.image.tag`; bump those in `values.yaml` (and cut a chart release) when you want a new operator version rolled out by `helm upgrade`.

---

## Helm Chart {#helm-chart}

Covers changes to `kubernetes/chart/`.

### When to update `appVersion`

`Chart.yaml` contains two version fields:

- `version` — the chart version; bump when templates or values change
- `appVersion` — the Docker image version the chart deploys; bump when releasing a new service version

If you are releasing a new service version alongside a chart update, update `appVersion` in `Chart.yaml` on the release branch before merging.

```bash
# 1. Cut a release branch from develop
git checkout develop && git pull origin develop
git checkout -b release/chart/v1.5.0

# 2. Bump the chart version (and appVersion if targeting a new service version)
#    Edit kubernetes/chart/Chart.yaml: version: "1.5.0" (and appVersion if needed)
git commit -am "chore: bump chart version to 1.5.0"
git push origin release/chart/v1.5.0

# 3. Open a PR release/chart/v1.5.0 -> main and merge it once checks pass.
#    Merging the PR automatically:
#      - tags main as chart/v1.5.0 (triggers chart-release.yml: package, push to GHCR + Pages)
#      - opens and auto-merges a PR to back-merge release/chart/v1.5.0 into develop
gh pr create --base main --head release/chart/v1.5.0 --title "release: chart v1.5.0" --fill
gh pr merge --merge --auto

# 4. Once the back-merge PR has merged, delete the release branch
git push origin --delete release/chart/v1.5.0
git branch -d release/chart/v1.5.0
```

The chart is published two ways from the same `chart/vX.Y.Z` tag:

- **HTTP repo** on GitHub Pages (via `helm/chart-releaser-action`) — the conventional `helm repo add` experience and the source Artifact Hub indexes.
- **OCI artifact** on GHCR — for users who prefer pulling charts from the same registry as images.

Install via the HTTP repo:

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
helm install hmac-manager zills/hmac-manager --version 1.5.0
```

Or via OCI:

```bash
helm install hmac-manager oci://ghcr.io/jzills/charts/hmac-manager --version 1.5.0
```

> **One-time setup for the HTTP repo:** create an empty `gh-pages` branch and enable
> GitHub Pages for it (Settings → Pages → Branch: `gh-pages`, folder `/`). The
> `pages` job in `chart-release.yml` then publishes `index.yaml`, the packaged
> chart, and `artifacthub-repo.yml` to the Pages root on each chart release.
> Point Artifact Hub at `https://jzills.github.io/hmac-manager`; because the repo
> metadata file is served at the root, the Verified Publisher badge works with no
> extra OCI metadata push.

---

## NPM Package (TypeScript client) {#npm-package}

Covers changes to the TypeScript client library under `client/lib/`.

```bash
# 1. Cut a release branch from develop
git checkout develop && git pull origin develop
git checkout -b release/npm/v2.7.0

# 2. Bump the version and stabilize
#    Edit client/lib/package.json: "version": "2.7.0"
git commit -am "chore: bump npm client version to 2.7.0"
git push origin release/npm/v2.7.0

# 3. Open a PR release/npm/v2.7.0 -> main and merge it once checks pass.
#    Merging the PR automatically:
#      - tags main as npm/v2.7.0 (triggers npm-release.yml: test, build, publish)
#      - opens and auto-merges a PR to back-merge release/npm/v2.7.0 into develop
gh pr create --base main --head release/npm/v2.7.0 --title "release: npm v2.7.0" --fill
gh pr merge --merge --auto

# 4. Once the back-merge PR has merged, delete the release branch
git push origin --delete release/npm/v2.7.0
git branch -d release/npm/v2.7.0
```

The pipeline sets the published version from the tag (`npm version --no-git-tag-version`), builds with Vite, and publishes to npmjs.com as [`hmac-manager`](https://www.npmjs.com/package/hmac-manager). Publishing is idempotent: if the version already exists on the registry, the publish step skips instead of failing.

**Required secret:** `NPM_TOKEN` (Settings → Secrets → Actions).

---

## Releasing Multiple Artifacts Together

When a service change also requires chart changes (e.g., new env var in values.yaml), release them in order:

1. **Service first** — merge `release/service/vX.Y.Z` → `main`, tag `service/vX.Y.Z`
2. **Chart second** — on the chart release branch, update `appVersion` to match, merge → `main`, tag `chart/vX.Y.Z`

This ensures the Docker image exists on Docker Hub before the chart that references it is published.

---

## Secrets Checklist

| Secret | Used by | Where to obtain |
|---|---|---|
| `NUGET_API_KEY` | `release.yml` | nuget.org → Account → API Keys (push-only, scoped to HmacManager) |
| `NPM_TOKEN` | `npm-release.yml` | npmjs.com → Access Tokens (granular, publish-only, scoped to `hmac-manager`) |
| `DOCKERHUB_USERNAME` | `service-release.yml`, `operator-release.yml` | Your Docker Hub username |
| `DOCKERHUB_TOKEN` | `service-release.yml`, `operator-release.yml` | Docker Hub → Account Settings → Security → Access Tokens |
| `RELEASE_PAT` | `tag.yml` | GitHub → Settings → Developer settings → Personal access tokens. Needs `repo` scope (Contents: write to push release tags, Pull requests: write to open + auto-merge the back-merge PR). Required because `GITHUB_TOKEN` can neither trigger the per-artifact release workflows via a tag push nor enable auto-merge. |

GHCR (Helm chart) uses the built-in `GITHUB_TOKEN` — no additional secret required.
