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
  `src/README.md`.
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

Tests mirror the source structure under `test/Unit/`. Shared test data and helpers are in `test/Unit/Common/`. The `src` project exposes internals to the `Unit` project via `InternalsVisibleTo`.

## CI/CD Pipelines

All pipelines are defined under `.github/workflows/`. Dependabot is configured separately at `.github/dependabot.yml`.

The per-artifact release pipelines (`release.yml` for the NuGet package, `npm-release.yml` for the TypeScript client npm package, `service-release.yml` for the ext-authz service image, `operator-release.yml` for the policy operator image, and `chart-release.yml` for the Helm chart) are driven by prefixed tags that `tag.yml` pushes on a release-branch merge — the end-to-end release process for each artifact is documented in [RELEASING.md](RELEASING.md). Each of these pipelines also creates a GitHub Release for its tag via `.github/scripts/create-release.sh` (uniform `HmacManager (<Kind>)` titles, marked latest, with install info and an auto-generated changelog scoped to the previous same-prefix tag).

---

### `pr.yml` — PR Validation

**File**: `.github/workflows/pr.yml`

**Trigger**: Any pull request opened or updated targeting `main` or `develop`.

**Purpose**: Gate merges by verifying the full test suite passes. Runs unit, operator, CI-script, and integration tests as parallel jobs so a failure in one does not block feedback from the others.

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

#### `ci-script-tests`
1. Checks out the repository.
2. Runs `shellcheck` over `.github/scripts/*.sh`.
3. Runs the offline shell test suites for the release helpers: `previous-tag.test.sh` and `create-release.test.sh` (stubbed `gh`, no network).

#### `integration-tests`
1. Checks out the repository.
2. Starts a Redis 7 instance using `supercharge/redis-github-action`.
3. Pings Redis to confirm it is accepting connections before proceeding.
4. Installs .NET `8.0.x` and `10.0.x`.
5. Restores dependencies in `test/Integration`.
6. Builds `test/Integration`.
7. Runs the integration test suite via `dotnet test`.

**Branch protection**: The `Unit Tests`, `Operator Tests`, `CI Script Tests`, and `Integration Tests` checks should be required to pass in GitHub → Settings → Branches for `main` and `develop` before a PR can be merged.

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
2. `npm ci`, `npm run build` (Vite), and `vitest run` in `client/lib`.

#### `publish` (runs only if `test` passes)
1. Extracts the semantic version by stripping the `npm/v` tag prefix (fails if not `X.Y.Z`).
2. Sets the package version from the tag (`npm version --no-git-tag-version`), builds, and publishes to https://registry.npmjs.org. If the version already exists on the registry, the publish is skipped (safe to re-run).
3. Creates a GitHub Release for the tag via `.github/scripts/create-release.sh` — titled `HmacManager (NPM) vX.Y.Z`, with install instructions and a changelog scoped to the previous `npm/v*` tag.

**Required secret**: `NPM_TOKEN` — an npmjs.com publish token for the `hmac-manager` package.

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

NuGet updates cover packages declared in `src/HmacManager.csproj` (e.g., `System.Runtime.Caching`). GitHub Actions updates cover the action versions pinned across all workflow files (e.g., `actions/checkout`, `actions/setup-dotnet`, `supercharge/redis-github-action`).
