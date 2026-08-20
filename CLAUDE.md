# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

All commands run from the respective project directory.

```bash
# Build the library
cd src && dotnet build

# Run all unit tests
cd test/Unit && dotnet test

# Run a single test class
cd test/Unit && dotnet test --filter "ClassName=Test_InMemory_ReplayAttack"

# Run a single test method
cd test/Unit && dotnet test --filter "FullyQualifiedName~MethodName"

# Run with coverage
cd test/Unit && dotnet test --collect:"XPlat Code Coverage"
```

The library targets `net8.0` and `net10.0`. Integration tests (`test/Integration`) require a running Redis instance on the default port.

The TypeScript client is a separate toolchain, in `client/lib`:

```bash
cd client/lib && npm ci

npm run typecheck     # tsc --noEmit over src and test — the only type gate
npm run build         # Vite; also the only thing that exercises src/index.ts
npx vitest run        # the suite
npx vitest run test/hmac-manager-verify.test.ts   # one file
```

Run all three. `npm run build` does not check types and `vitest` does not either — see the `pr.yml` section below.

## Architecture Overview

HmacManager is an ASP.NET Core HMAC authentication library. It supports both server-side request verification and client-side request signing via `HttpClient`. The two primary integration paths are:

1. **Server (verifying)**: Register via `services.AddHmacManager(...)` + `builder.AddHmac(...)`. Requests are validated by `HmacAuthenticationHandler` (in `src/Mvc/`).
2. **Client (signing)**: Register via `httpClientBuilder.AddHmacHttpMessageHandler(...)`. `HmacDelegatingHandler` signs outgoing requests automatically.

### Core signing flow

`IHmacManager` (`src/Components/HmacManager.cs`) is the central type. `SignAsync(HttpRequestMessage)` creates an `Hmac`, computes a signature, and attaches headers. `VerifyAsync(HttpRequestMessage)` parses incoming headers, recomputes the signature, and compares.

Signature computation lives in `HmacSignatureProvider` → `HmacFactory` → hash generators in `src/Components/Hashing/Generators/`. The signing content string (method + URI + date + public key + content hash + scheme header values + nonce) is built by implementations of `ISigningContentBuilder`.

### Policies and schemes

- **`HmacPolicy`** (`src/Policies/`) is the top-level configuration unit: it holds `KeyCredentials`, hash `Algorithms`, an optional `Nonce` cache config, and a `SchemeCollection`.
- **`Scheme`** (`src/Schemes/`) is a named set of required HTTP headers whose values are included in the signature, enabling scoped authentication contexts.
- Policies are stored in `IHmacPolicyCollection`, which is registered as either singleton (static) or scoped (dynamic/DB-driven).

### Nonce caching

`INonceCache` (`src/Caching/`) prevents replay attacks by storing used nonces with a TTL. Two implementations exist: `MemoryNonceCache` (in-process) and `DistributedNonceCache` (Redis). The `Nonce` config on a policy selects which to use.

### Logging

Every log message the library can emit is declared in one place: `src/Diagnostics/HmacLog.cs`, an
`internal static partial class` of `[LoggerMessage]` source-generated methods. The two container
projects have their own equivalents — `kubernetes/operator/Diagnostics/OperatorLog.cs` and
`kubernetes/service/Diagnostics/ServiceLog.cs`.

Rules when adding a message:

- **Declare it in the catalogue, never inline.** Call sites stay one line (`HmacLog.X(Logger, …)`),
  and the catalogue stays reviewable as a whole — which is what makes "no message can leak a key"
  checkable rather than aspirational.
- **Event ids are a contract.** Take the next free id in the right range (documented at the top of
  each catalogue); never reuse an id for a different event. The library's ids are published in
  `site/content/docs/reference/log-events.md`, which is the page a reader is pointed at from
  `src/README.md` and from the logging docs — add the id there in the same change.
- **Never log a private key.** `test/Unit/Diagnostics/` asserts this over the full sign/verify path
  with all levels enabled.
- **Levels**: `Information` only for events an operator needs unprompted (the live policy set
  changing); `Warning` for a recognized signing attempt that was rejected (expired, replayed,
  mismatched) and for server-side faults (failed reloads, an unregistered cache); `Debug` for
  per-request outcomes and for unrecognized input a caller can send unbounded (no header,
  unparseable headers, an unknown policy name); `Trace` for signing content and signatures. Do not
  put caller-controlled rejections at `Warning` — an edge deployment must not be drivable to
  unbounded `Warning` volume.

Loggers are never required. Public types (`HmacManager`, `HmacManagerFactory`,
`HmacDelegatingHandler`, `HmacAuthenticationContextProvider`) keep their original constructor and
gain an overload that takes a logger; the original delegates with `NullLogger`. DI prefers the
logging overload, so applications get logging without opting in and binary compatibility is
preserved.

`HmacPolicyCollectionReloader` is the exception to normal DI: it is constructed during
`AddHmacManager` so no configuration change is missed before the first request, which is before any
`IServiceProvider` exists. It is registered as an `IHostedService` purely so the container can hand
it the real logger (`UseLogger`) and so it can report the live policy set at startup.

### DI wiring

`IServiceCollectionExtensions.AddHmacManager()` (`src/Mvc/Extensions/`) registers all internal services. `IHmacManagerFactory` is the DI-resolvable entry point to obtain an `IHmacManager` for a named policy at runtime.

### Key types at a glance

| Type | Location | Purpose |
|---|---|---|
| `IHmacManager` | `src/Components/Interfaces/` | Sign / verify requests |
| `IHmacManagerFactory` | `src/Components/Interfaces/` | Resolve manager by policy name |
| `HmacPolicy` | `src/Policies/` | Top-level auth configuration |
| `Scheme` | `src/Schemes/` | Named header set included in signature |
| `HmacResult` | `src/Components/` | Result of sign/verify (success + `Hmac` snapshot) |
| `HmacDelegatingHandler` | `src/Mvc/` | Auto-signs outgoing `HttpClient` requests |
| `HmacAuthenticationHandler` | `src/Mvc/` | ASP.NET Core auth scheme handler |
| `HmacEvents` | `src/Mvc/` | Hooks: `OnValidateKeys`, `OnAuthSuccess`, `OnAuthFailure` |

### Test layout

Tests mirror the source structure under `test/Unit/`. Shared test data and helpers are in `test/Unit/Common/`. The `src` project exposes internals to the `Unit` project via `InternalsVisibleTo`. The TypeScript client has its own flat suite under `client/lib/test/`.

**`test/fixtures/signing-parity.json` is the contract between the two.** It holds requests with their
expected signing content and signature, and both suites assert against it —
`test/Unit/Components/SigningContent/Test_SigningContentParity.cs` and
`client/lib/test/signing-parity.test.ts`. It is the only thing that stops the two implementations
drifting into being perfectly self-consistent and unable to talk to each other, which is what had
already happened: the signing content was hashed as UTF-8 in .NET and as one byte per UTF-16 code
unit in TypeScript, and every request whose scheme headers carried a non-ASCII character got a
different signature from each, with nothing to distinguish the rejection from a forgery.

It is generated by `test/fixtures/gen-signing-parity.mjs`, which deliberately shares no code with
either implementation — agreeing with it is evidence rather than a tautology. **Add a case by editing
the generator and regenerating. Never regenerate to make a failure go away**: a diff in that file is
a change to the wire format, and noticing it is the entire point.

`client/lib/test/node-round-trip.test.ts` covers what an in-process test cannot — a signed request
over a real socket into a real `node:http` server, which is what exercises `fromNodeRequest`'s URL
reconstruction and body handling.

### Sample layout

Every sample under `samples/` is the same exchange — a client signing requests to an API
that verifies them — with **one** thing changed, so a diff between two of them is the
feature. That only holds if everything else is identical, so they all share one key pair,
one policy name (`MyPolicy`), one scheme (`RequireAccountAndEmail`) and one pair of
scheme headers. Changing those in one sample without a reason breaks the comparison that
makes the set worth having.

- **One project per directory, no `src/` level.** `Api/Api.csproj`, not
  `Api/src/Api.csproj`, so `dotnet run --project Api` — the command every README has
  always printed — actually works.
- **One README per sample, at its root.** The per-project `Api/README.md` and
  `Web/README.md` files are gone; they duplicated the docs site and were what went stale
  (two were empty, two said "We're working here"). Explanation belongs in
  `site/content/docs/`; a sample README says what to run and what to look at.
- **Ports are assigned, not scaffolded**: `51N0` for the Api and `51N1` for the client,
  `5200` for the Node API. They are listed in `samples/README.md`. Two samples previously
  shared a port and neither could run beside the other.
- **Plain HTTP.** No development certificate, and no `NODE_TLS_REJECT_UNAUTHORIZED=0` in
  a sample teaching people to authenticate requests.
- **Link the client library, never a tarball.** `"hmac-manager": "file:../../../client/lib"`.
  A `.tgz` reference is gitignored, so the sample cannot install from a clean clone.

### Documentation

User-facing documentation lives in **one** place: `site/content/docs/`, published to
<https://jzills.github.io/hmac-manager/>. The READMEs are deliberately thin — each is a summary,
an install snippet and links into the site — because each one is rendered by a host that shows
nothing else (`src/README.md` on nuget.org via `PackageReadmeFile`, `client/lib/README.md` on
npmjs.com, `kubernetes/chart/README.md` on Artifact Hub, the two `kubernetes/*/README.md` on Docker
Hub). Those three keep their reference tables, since those platforms will not follow a link.

**Document a behaviour change in `site/content/docs/`, not in a README.** Sections map to surfaces:
`concepts/` is the vocabulary shared by all three implementations, then `dotnet/`, `kubernetes/`
and `client/`, with lookup tables under `reference/`. Prose is hand-authored — there is no
generator from the READMEs, so a claim in the docs is only as accurate as the person who wrote it;
verify API names against the source before writing them down.

`site/` is a self-contained Hugo site; `hugo server` from that directory previews it. See the
`pages.yml` section below for how it is built and published, and why the `gh-pages` branch needs
care.

## CI/CD Pipelines

All pipelines are defined under `.github/workflows/`. Dependabot is configured separately at `.github/dependabot.yml`.

`pages.yml` builds and publishes the documentation site under `site/` (Hugo + Hextra) to the `gh-pages` branch, which it shares with the Helm chart HTTP repository — see its section below before changing anything that writes to that branch.

The per-artifact release pipelines (`release.yml` for the NuGet package, `npm-release.yml` for the TypeScript client npm package, `service-release.yml` for the ext-authz service image, `operator-release.yml` for the policy operator image, and `chart-release.yml` for the Helm chart) are driven by prefixed tags that `tag.yml` pushes on a release-branch merge — the end-to-end release process for each artifact is documented in [RELEASING.md](RELEASING.md). Each of these pipelines also creates a GitHub Release for its tag via `.github/scripts/create-release.sh` (uniform `HmacManager (<Kind>)` titles, marked latest, with install info and an auto-generated changelog scoped to the previous same-prefix tag).

---

### `pr.yml` — PR Validation

**File**: `.github/workflows/pr.yml`

**Trigger**: Any pull request opened or updated targeting `main` or `develop`.

**Purpose**: Gate merges by verifying the full test suite passes. Runs unit, operator, client, CI-script, sample, and integration tests as parallel jobs so a failure in one does not block feedback from the others.

**Jobs**:

#### `unit-tests`
1. Checks out the repository.
2. Installs .NET `8.0.x` and `10.0.x` (matching the library's target frameworks).
3. Restores dependencies in `test/Unit`.
4. Builds `test/Unit`.
5. Runs the unit test suite via `dotnet test`.

#### `operator-tests`
1. Checks out the repository.
2. Installs .NET `8.0.x` and `10.0.x`.
3. Restores dependencies in `test/Operator`.
4. Builds `test/Operator`.
5. Runs the operator test suite via `dotnet test` (rendering, mapping, validation, and status reconciliation for the `HmacPolicy` CRD controller under `kubernetes/operator/`).

#### `client-tests`
1. Checks out the repository.
2. Installs Node `24.x`, with the npm cache keyed to `client/lib/package-lock.json`.
3. `npm ci` in `client/lib`.
4. `npm run typecheck` — `tsc --noEmit` over `src` and `test`. This is the only step here that checks types: vitest transpiles through esbuild without checking, and `vite-plugin-dts` prints `tsc`'s errors during the build but the build exits `0` regardless, so before this step a type error passed both.
5. `npm run build` — **not redundant with step 6**. Vitest only loads modules its tests import, so nothing exercises `src/index.ts`, the package entry point that `npm-release.yml` publishes; a broken import there fails the build and passes the tests.
6. Runs the TypeScript client suite via `npx vitest run`.

Mirrors the `test` job in `npm-release.yml`, which was the only place these ran until this job existed — at release time, on a tag, after the code had already merged.

The gate runs against `client/lib/tsconfig.typecheck.json`, which exists only because `tsconfig.json` is also what `vite-plugin-dts` reads: widening that file's `include` to cover the tests would emit a `.d.ts` for every test file into `dist/`.

#### `ci-script-tests`
1. Checks out the repository.
2. Runs `shellcheck` over `.github/scripts/*.sh`.
3. Runs the offline shell test suites for the release helpers: `previous-tag.test.sh` and `create-release.test.sh` (stubbed `gh`, no network).

#### `sample-builds`
1. Checks out the repository.
2. Installs .NET `8.0.x` / `10.0.x` and Node `24.x`.
3. Runs `.github/scripts/verify-samples.sh`, which builds every `.csproj` under
   `samples/`, then runs the two Node samples end to end and asserts on their output —
   including the replay and tampering rejections.

Nothing in CI referenced `samples/` before this job, and it showed: the JavaScript
client sample depended on a `.tgz` that is gitignored and was never committed, so
`npm install` failed on any clean checkout; one sample was pinned to a released package
four minor versions behind the source beside it; and the run command in every README
named a directory that held no project. A build alone catches only the second, which is
why the Node samples are **run** rather than compiled. The script is also the local
command — `bash .github/scripts/verify-samples.sh`.

#### `integration-tests`
1. Checks out the repository.
2. Starts a Redis 7 instance using `supercharge/redis-github-action`.
3. Pings Redis to confirm it is accepting connections before proceeding.
4. Installs .NET `8.0.x` and `10.0.x`.
5. Restores dependencies in `test/Integration`.
6. Builds `test/Integration`.
7. Runs the integration test suite via `dotnet test`.

**Branch protection**: The `Unit Tests`, `Operator Tests`, `Client Tests`, `CI Script Tests`, `Sample Builds`, and `Integration Tests` checks should be required to pass in GitHub → Settings → Branches for `main` and `develop` before a PR can be merged.

---

### `release.yml` — Pack and Publish to NuGet

**File**: `.github/workflows/release.yml`

**Trigger**: Push of a tag matching `nuget/v*`. These tags are not pushed by hand — `tag.yml` creates them when a `release/vX.Y.Z` PR is merged into `main` (see [RELEASING.md](RELEASING.md)). The pipeline never runs on a branch push.

**Purpose**: Validate the release candidate and publish the NuGet package. The version is derived from the tag name, making it explicit and auditable without requiring commit message conventions.

**Tag naming convention**: Release tags follow the pattern `nuget/vX.Y.Z` (e.g., `nuget/v2.7.0`). The `publish` job strips the `nuget/v` prefix and fails immediately if the remaining version does not match `X.Y.Z`.

**Jobs**:

#### `test`
1. Checks out the repository.
2. Starts a Redis 7 instance and verifies it is running.
3. Installs .NET `8.0.x` and `10.0.x`.
4. Runs the full unit test suite (`test/Unit`).
5. Runs the full integration test suite (`test/Integration`).

#### `publish` (runs only if `test` passes)
1. Checks out the repository with full git history (`fetch-depth: 0`).
2. Extracts the semantic version from the tag name by stripping the `nuget/v` prefix. Validates the result matches `X.Y.Z` and exits with an error if not.
3. Installs .NET `8.0.x` and `10.0.x`.
4. Packs the library in Release configuration with the extracted version: `dotnet pack --configuration Release -p:Version=X.Y.Z`.
5. Pushes the `.nupkg` to NuGet Gallery using `dotnet nuget push`. The `--skip-duplicate` flag prevents failure if the version was already published (safe to re-run).
6. Creates a GitHub Release for the tag via `.github/scripts/create-release.sh` — titled `HmacManager (NuGet) vX.Y.Z`, marked as latest, with install instructions and a changelog auto-generated since the previous `nuget/v*` tag.

**Required secret**: `NUGET_API_KEY` must be set in GitHub → Settings → Secrets and variables → Actions. Obtain this from nuget.org → Account → API Keys. Scope the key to the `HmacManager` package with push-only permissions.

**Branch protection**: Gate the release at PR-merge time — require the `pr.yml` checks (`Unit Tests`, `Operator Tests`, `Integration Tests`) on `main` so a `release/*` PR only merges (and thus only tags) when green. The `nuget/v*` tag then triggers this pipeline, which re-runs `test` before `publish`.

**Typical workflow**:
```bash
# Cut a release branch, bump src/HmacManager.csproj <Version>, then open a PR to main.
# Merging the PR tags nuget/vX.Y.Z, which triggers this pipeline. See RELEASING.md.
git checkout -b release/v2.7.0
# bump <Version>, commit, push, then: gh pr create --base main ... && gh pr merge
```

---

### `npm-release.yml` — Build and Publish to npm

**File**: `.github/workflows/npm-release.yml`

**Trigger**: Push of a tag matching `npm/v*`, created by `tag.yml` when a `release/npm/vX.Y.Z` PR is merged into `main` (see [RELEASING.md](RELEASING.md)).

**Purpose**: Test, build, and publish the TypeScript client library (`client/lib/`) to npmjs.com as `hmac-manager`.

**Jobs**:

#### `test`
1. Checks out the repository and installs Node `24.x` (with npm cache keyed to `client/lib/package-lock.json`).
2. `npm ci`, `npm run typecheck`, `npm run build` (Vite), and `vitest run` in `client/lib` — the same four steps as the `client-tests` job in `pr.yml`.

#### `publish` (runs only if `test` passes)
1. Extracts the semantic version by stripping the `npm/v` tag prefix (fails if not `X.Y.Z`).
2. Sets the package version from the tag (`npm version --no-git-tag-version`), builds, and publishes to https://registry.npmjs.org. If the version already exists on the registry, the publish is skipped (safe to re-run).
3. Creates a GitHub Release for the tag via `.github/scripts/create-release.sh` — titled `HmacManager (NPM) vX.Y.Z`, with install instructions and a changelog scoped to the previous `npm/v*` tag.

**Required secret**: `NPM_TOKEN` — an npmjs.com publish token for the `hmac-manager` package.

---

### `pages.yml` — Build and Publish the Documentation Site

**File**: `.github/workflows/pages.yml`

**Triggers**: pushes to `develop` and pull requests touching `site/**`, `assets/**` or the workflow itself, plus `workflow_dispatch`. Built on both; published only from `develop`, so a PR that breaks the site fails before deploy time.

**Purpose**: build the Hugo site under `site/` and publish it to the `gh-pages` branch.

**The constraint that shapes this pipeline**: `gh-pages` is *also* the Helm chart HTTP repository. It holds `index.yaml`, the chart `.tgz` files and `artifacthub-repo.yml`, published by `chart-release.yml`. `index.yaml` pins every chart to a **root-level URL with a content digest**, and the tarballs exist nowhere else in the repository (`**/*.tgz` is gitignored on source branches). Those files must keep being served from `/` byte for byte or `helm repo add zills https://jzills.github.io/hmac-manager` breaks for everyone already using it.

**Jobs**:

#### `build`
1. Checks out the repository.
2. Installs Go via `go-version-file: site/go.mod` — the Hextra theme is a Hugo module, which is a Go module. `site/go.mod` is the only Go module in this repository, so the theme is isolated from the .NET and npm dependency graphs by construction. It is deliberately **not** in `dependabot.yml`; the theme is pinned on purpose.
3. Installs Hugo **extended** (pinned by `HUGO_VERSION`; Hextra requires the extended build).
4. `hugo --gc --minify` in `site/`.
5. **Asserts the stylesheets were built.** Hugo resolves the theme's CSS through `resources.Get`, which returns nothing when the module's assets are missing — and the build still *succeeds*, publishing an unstyled site. The step fails if `public/css/compiled/*.css` is absent or under 10 KB, or if the fingerprinted `hm-theme*.css` is missing.
6. Uploads `site/public` as an artifact (develop only).

#### `publish` (develop only, needs `build`)
1. Checks out with full history and downloads the artifact.
2. Adds a `gh-pages` worktree, records the sha256 of `index.yaml` and every `.tgz`, then `rsync -a --delete` the site in, **excluding** `index.yaml`, `*.tgz`, `artifacthub-repo.yml`, `.git` and `.nojekyll`. `--delete` prunes site pages that no longer exist; the excludes are what keep the chart repository intact.
3. Re-hashes those files and **fails the job** if any changed — the guard is against reality, not an assumption.
4. `touch .nojekyll` (Pages runs branch-served content through Jekyll otherwise, which drops underscore-prefixed paths).
5. Commits and pushes, skipping cleanly when nothing changed.

**Concurrency**: the job shares `concurrency: { group: gh-pages }` with the `pages` job in `chart-release.yml`. Both do fetch/worktree/commit/push against the same branch, so without it they can interleave and one loses its commit.

**Branch protection**: consider requiring the `Build` check on `main` and `develop` alongside the existing five.

**Site generators**: `site/tools/gen-marks.py` writes `site/assets/hm-wordmark.svg`, `site/assets/hm-mark.svg` and the README's `assets/logo.svg`; `site/tools/gen-favicons.mjs` rasterizes the favicon set into `site/static/` through headless Chromium. Both are run **by hand** and their output committed — no build step invokes them, and CI installs neither toolchain. See `site/tools/README.md`.

---

### `codeql.yml` — Static Security Analysis

**File**: `.github/workflows/codeql.yml`

**Triggers**:
- Any pull request targeting `main` or `develop`.
- Weekly on Monday at midnight UTC (scheduled via cron: `0 0 * * 1`).

**Purpose**: Detect security vulnerabilities and code quality issues in C# using GitHub's CodeQL engine. Free for public repositories. Results appear in GitHub → Security → Code scanning alerts.

**Job**: `analyze`
1. Checks out the repository.
2. Initializes the CodeQL analyzer for the `csharp` language.
3. Installs .NET `8.0.x` and `10.0.x`.
4. Builds the library (`src/`) so CodeQL can trace the compiled output.
5. Runs the CodeQL analysis and uploads results to GitHub Security.

**Required permissions**: The job declares `security-events: write` so it can upload findings to the Security tab. No secrets required — this uses the built-in `GITHUB_TOKEN`.

---

### Dependabot — Automated Dependency Updates

**File**: `.github/dependabot.yml`

**Purpose**: Automatically open pull requests when dependencies are out of date, keeping the project current without manual monitoring. PRs raised by Dependabot go through the normal `pr.yml` validation before they can be merged.

**Update targets**:

| Ecosystem | Directory | Schedule |
|---|---|---|
| NuGet packages | `/` | Weekly |
| GitHub Actions | `/` | Weekly |
| npm | `/client/lib` | Weekly |
| npm | `/client/sample` | Weekly |

The samples under `/samples` deliberately have no npm entry: their only dependency is a
`file:` link to `client/lib`, so there is nothing external to pin and no lockfile is
committed. `sample-builds` keeps them honest instead.

NuGet updates cover packages declared in `src/HmacManager.csproj` (e.g., `System.Runtime.Caching`). GitHub Actions updates cover the action versions pinned across all workflow files (e.g., `actions/checkout`, `actions/setup-dotnet`, `supercharge/redis-github-action`).
