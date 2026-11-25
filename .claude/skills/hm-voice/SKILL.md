---
name: hm-voice
description: Use when writing or rewording any text HmacManager ships to a reader — the docs site (site/content/), the landing page, the READMEs nuget.org, npm, Docker Hub and Artifact Hub render, release notes (with hm-release-notes for their shape), PR titles and descriptions, and log message text. The house rules for sentences, spelling, naming and layout, shared with kx and snoop so the projects read alike.
---

# HmacManager's voice

HmacManager's text is plain, factual and short. Say what something does and what to do next, without marketing language. The rules are kx's and snoop's, so a reader moving between the three projects meets one voice.

## Sentences

Every sentence is a complete American English sentence.

1. **Never start a sentence with a code span.** That covers types, methods, headers, flags, paths and values. Lead with a word instead.
   - "`AddNonceCache` replaces one cache type." → "Call `AddNonceCache` to replace one cache type."
   - "`Hmac-Nonce` is a GUID." → "The `Hmac-Nonce` header carries a GUID."
2. **Never start a sentence with a lowercase name.** The npm package is `hmac-manager`; open with "The npm client" or "The `hmac-manager` package" instead.
3. **Write no fragments.** Captions, card subtitles, list items and lead-ins are sentences too: "Sign a request from Node." not "Signing from Node."
4. **Use American spelling:** behavior, color, recognize, normalize, canceled, license (noun and verb). Comments in code follow the file they're in; anything a reader sees follows this rule.
5. **Table cells and headings may be phrases.** Keep each cell one phrase or one clause, with no trailing period.

## Voice

- Address the reader as "you", and use the imperative for steps.
- State facts, not claims: "Each claim is one `SET NX`," not "blazing fast".
- Don't promise what the code doesn't do. Check every API name, default, header and log event id against the source before writing it down; the docs are hand-written and are only as accurate as their author.
- Name the project "HmacManager". Name the parts by what a reader installs: the NuGet package, the npm client, the ext-authz service, the operator, the Helm chart, the Redis package.
- Public API names (`AddHmacManager`, `IHmacManager`, `TryAddAsync`, `NonceCache`) are the reader's vocabulary and appear in code formatting. Internal ones (`HmacPolicyCollectionReloader`, `ExtAuthzHandler`) stay out of anything but PRs and comments.
- Never write "we".

## Layout

- **One sentence per line in Markdown, never hard-wrapped.** A list item that runs to a second sentence continues on the next line, indented two spaces. Diffs then show the sentence that changed. Release notes are stricter: see `hm-release-notes`.
- **Code blocks are 80 columns or narrower,** because GitHub, nuget.org and npm never wrap them.
- **Versions are written with their v** in prose: v2.13.0, never 2.13.0. A command keeps the registry's form: `dotnet add package HmacManager --version 2.13.0`.

## Before and after

> ✗ `NonceDistributedCache` now writes a relative TTL computed after its read, fixing #153.
>
> ✓ A replay inside the window is now refused when the distributed cache is Redis, which used to drop an entry with under a second left.

> ✗ Behaviour of the authorisation handler on HTTP/2.
>
> ✓ Consolidated headers verify on HTTP/2, whose header names arrive in lowercase.
