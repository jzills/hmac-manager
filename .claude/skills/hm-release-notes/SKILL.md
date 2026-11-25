---
name: hm-release-notes
description: Use when writing or revising release-notes/<artifact>/vX.Y.Z.md for any HmacManager artifact (NuGet package, npm client, Helm chart, ext-authz service, operator, Redis package), the Highlights that open its GitHub release and its entry on the site's Releases page. Load hm-voice too.
---

# Writing HmacManager release notes

Every release has one notes file, `release-notes/<artifact>/vX.Y.Z.md`. The path is the release's tag with `.md` on the end, so `nuget/v2.13.0` reads `release-notes/nuget/v2.13.0.md`. The file is read in two places:

- **GitHub:** `.github/scripts/create-release.sh` puts it under a `## Highlights` heading, above the install commands and the generated list of merged PRs.
- **The site:** the Releases page (`site/content/releases.md`, the `hm-release-notes` shortcode) shows every file, one section per artifact, newest first.

The notes only have to answer two questions: what's new for me, and do I have to change anything? The PR list below them on GitHub carries the rest.

| Artifact | Tag prefix | Directory the notes describe |
|---|---|---|
| NuGet package | `nuget/` | `src/HmacManager/` |
| npm client | `npm/` | `client/lib/` |
| Helm chart | `chart/` | `kubernetes/chart/` |
| ext-authz service | `service/` | `kubernetes/service/` and the library it ships |
| Operator | `operator/` | `kubernetes/operator/` |
| Redis package | `redis/` | `src/HmacManager.StackExchangeRedis/` |

The NuGet package was tagged `v2.1.0` to `v2.5.0` before the prefixes existed. Those notes live under `release-notes/nuget/` too.

## The shape

```markdown
<Lead: two or three sentences on what this release is about.>

- **<What you notice, in a few words>:** <One complete sentence, with one example.>
- **<…>:** <…>.

**Breaking:** <If …, do …> — or **After upgrading:** <If …, do …>, only when there is something to do.
```

- **Lead:** two or three sentences, under about 60 words. Say why the release matters to someone running this artifact, not how it's built. A one-fix patch can be the lead alone.
- **Bullets:** one per change a reader would notice, at most six. Each is a bold label of a few words, a colon, then one complete sentence with one example in backticks: a call, a header, a values key, or the log line it writes.
- **Say when:** if it happens only sometimes, name the condition: "When a body arrives with no `Content-Length` header, …".
- **Open on the outcome:** "A chunked upload now verifies," not "Fixed the authentication handler."
- **Label and sentence both open with a capital,** and the sentence ends with a period. The sentence never opens with inline code (see `hm-voice`).
- **What to do:** a closing line only when something breaks, changes on the wire, or needs a step. Label it **Breaking:** or **After upgrading:**, and open with "If", naming who is affected and what to do. A change to the signing content is always one of these: a signer and a verifier on different versions disagree.
- **Length:** about 150 words. Over 200 means something belongs in the docs instead.

## Each artifact's own release

The artifacts release separately from one repository, so a release's range of commits includes work on the others. Describe only what reaches a reader of **this** artifact:

- **The service and the chart** ship the library inside them. A library fix is a service note when it changes what the service accepts or rejects; say which NuGet version it carries.
- **The chart** changes when its templates or values change, and with every new service image. A chart release that only moves to a new image says so in one line: "Deploys the ext-authz service v0.5.1, which …".
- **A release with nothing a reader would notice** — a rebuild, a version bump — is the lead alone: "A rebuild with no change you would notice." Never pad it.

## Leave out

- **Mechanism and history:** which method changed, why the bug existed, what the PR's review found. Name the new behavior and stop.
- **Repository chores:** CI, docs, samples, project moves, tests. A reader of the package never sees them.
- **Anything the PR list says word for word.**
- **Internal names** (see `hm-voice`). Public API, headers, values keys and log event ids are fine.

## Format rules

- Each paragraph and each bullet is a single line. GitHub turns every newline inside one into a `<br>`.
- No headings: the release page and the site supply them.
- Second person, American spelling, versions with their v.

## Worked examples

A library minor (`release-notes/nuget/v2.13.0.md`):

```markdown
Requests whose body arrives without a length now verify, and a policy that only looks shared gets a warning at startup.

- **Chunked bodies:** A request with no `Content-Length` header, such as a chunked upload or HTTP/2 through a proxy, now verifies, and your endpoint still receives the body.
- **HTTP/2 headers:** With `EnableConsolidatedHeaders()`, a request whose header names arrive in lowercase gets a verification result instead of a 500.
- **A replay warning at startup:** A policy on the `Distributed` nonce cache with only the in-process `MemoryDistributedCache` behind it logs event 1203 once, since a replay that reaches another instance is accepted.
```

A chart release that moves to a new image (`release-notes/chart/v0.5.1.md`):

```markdown
Deploys the ext-authz service v0.5.1, which verifies a request whose body arrives without a `Content-Length` header.
```

## Before you commit

1. Build the release's range: `git log --no-merges --format='- %s' <previous tag>..<this tag> -- <directory>` gives the commits that touched the artifact. Read the PRs behind them, not only their titles.
2. Check every API name, values key, header and event id against the source at the tag (`git show <tag>:<path>`).
3. Count the words, and check the lead is three sentences or fewer.
4. Preview the site with `hugo server` in `site/` and open `/releases/`.
