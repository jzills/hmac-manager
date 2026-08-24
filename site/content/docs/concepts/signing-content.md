---
title: Signing content
description: Exactly what string gets hashed, in what order, and why a mismatch happens.
weight: 3
---

The signature is an HMAC over a single string built from the request. Both
sides build that string independently and compare the results, so **every
segment has to match exactly**. Almost every signature mismatch is really a
signing-content mismatch.

## The format

Segments are joined with `:` in this order:

```
{method}:{path and query}:{authority}:{dateRequested}:{publicKey}[:{contentHash}][:{schemeHeaderValues…}]:{nonce}
```

| Segment | Value | Present |
| --- | --- | --- |
| `method` | `GET`, `POST`, … | always |
| `path and query` | `/orders?page=2` | always |
| `authority` | `api.example.com`, with port if non-default | always |
| `dateRequested` | Unix time in **milliseconds** | always |
| `publicKey` | the policy's public key GUID, canonical **lowercase** | always |
| `contentHash` | hash of the request body | only when the body is **non-empty** |
| scheme header values | the values, **trimmed**, in the order the scheme declares | only with a scheme |
| `nonce` | a GUID generated per request, canonical **lowercase** | always |

A concrete example — a `GET` with no body and no scheme:

```
GET:/orders:api.example.com:1723651200000:00000000-0000-0000-0000-000000000001:6f9619ff-8b86-d011-b42d-00c04fc964ff
```

That string is hashed with the signing algorithm and the private key, and the
result goes into `Authorization: Hmac <signature>`.

The .NET and TypeScript implementations build the same string. The authority
segment is built from `Uri.IdnHost` in .NET and `URL.host` in the browser,
both of which include a non-default port and omit a default one, which is
what makes a request signed in a browser verify in .NET.

For an internationalized domain name, every side signs the **punycode** form
(`xn--caf-dma.example.com`, not `café.example.com`) — the form that actually
travels in the `Host` header, since DNS and HTTP require it there.

This used to be true of every side *except* a .NET signer. A verifier rebuilds
the URI from the `Host` header it received, which is already punycode, so it
has always produced the punycode form; the TypeScript client gets it from
`URL.host`, which applies IDNA unconditionally. Only .NET's signing path read
`Uri.Authority` off the URL as the caller wrote it, so an `HttpClient`
configured with `https://café.example.com` signed a host **nothing else
reproduced — including a .NET verifier**. It now uses `Uri.IdnHost`.

That makes this a fix rather than a wire-format change: it turns requests that
were rejected into requests that verify, and a signer on the new version works
against a verifier on either. A caller that already configured the punycode
form was never affected in the first place.

An IPv6 literal keeps its brackets — `[::1]:8080`, not `::1:8080` — which is
what `URL.host` produces and what `Uri.Authority` produced before. Only the
IDN signing case changed.

The public key is written in the canonical lowercase GUID form regardless of how
the policy spells it. .NET gets that for free — `KeyCredentials.PublicKey` is a
`Guid`, so the segment is always `Guid.ToString()` — and the TypeScript client,
which takes a string, lowercases a canonical GUID to match. It matters because
`NEWID()`, PowerShell and the Azure portal all produce uppercase GUIDs, and a
client that signed one verbatim agreed with every other copy of itself while
failing against .NET on every request.

The nonce is normalized the same way, and for the same reason. .NET parses the
`Hmac-Nonce` header into a `Guid`, so the segment is always lowercase whatever
case arrived; the TypeScript client lowercases the parsed value to match. Both
libraries emit lowercase nonces already, so this only matters for a
third-party signer — one written in Go or Python against this page — which
would otherwise be accepted by one implementation and rejected by the other.

Scheme header values are **trimmed** before they are signed. The Fetch
`Headers` class strips surrounding whitespace on the way in per specification,
and HTTP itself treats optional whitespace around a field value as
insignificant, so a value signed untrimmed would not match what the parser on
the receiving end delivers.

**An empty body contributes no segment.** A `POST` whose body is present but
zero-length is treated exactly like one with no body at all — there is no
content-hash segment on either side. Hashing the empty string instead would
produce a segment no verifier reproduces.

The string is hashed as **UTF-8**. Every segment above is ASCII, where the
encoding makes no difference — except the values a scheme contributes, which are
whatever the caller put in those headers.

## Cross-implementation parity

Nothing about "both implementations build the same string" is enforced by
sharing code, because they share none. It is enforced by
`test/fixtures/signing-parity.json`: a set of requests with their expected
signing content and signature, generated by a script that shares no code with
either implementation, and asserted by both test suites.

Adding a case means adding it to `test/fixtures/gen-signing-parity.mjs` and
regenerating. Regenerating after changing an *implementation* defeats the point
— the fixture exists to notice that change.

It is worth the ceremony. Before it existed, the signing content was hashed as
UTF-8 in .NET and as one byte per UTF-16 code unit in TypeScript. Both were
self-consistent, both suites were green, and any request whose scheme headers
carried a non-ASCII character got a different signature from each — with nothing
to distinguish the rejection from a forged request.

## The URI must be absolute

The authority is part of the content, so it has to be known at signing time.
Signing an `HttpRequestMessage` that carries a relative URI throws
`AbsoluteUriException` rather than signing something the server cannot
reproduce.

This is easy to hit by accident: an `HttpClient` with a `BaseAddress` will
happily send `client.GetAsync("/orders")`, and the request only becomes
absolute *inside* the client. If you sign manually before that point, you sign
`/orders` with no authority while the server verifies against the full URL.

Using [`AddHmacHttpMessageHandler`](../../dotnet/http-client/) avoids it
entirely — the handler signs after the `BaseAddress` has been applied.

## Things that break a match

| Cause | Looks like |
| --- | --- |
| A proxy rewriting `Host` or the path | mismatch on every request |
| Signing a relative URI | `AbsoluteUriException`, or a universal mismatch |
| Reading the request body before signing | body hash differs; the stream was already consumed |
| Clock skew past the replay window | rejected as expired, not as a mismatch |
| Different algorithms on each side | mismatch on every request |
| A scheme header added *after* signing | mismatch, or a signing failure |
| A percent-encoded unreserved character in the path (`%41` for `A`) | mismatch on that request only |
| A URL ending in a bare `?` with no query | mismatch on that request only |

The path/query pair is where .NET and the browser disagree in ways that are
narrow enough to document rather than fix, since normalizing either side
changes the wire format for a case nobody is deliberately relying on:

- **Percent-encoded unreserved characters.** `Uri.PathAndQuery` unescapes the
  RFC 3986 §2.3 unreserved set (`A-Za-z0-9-._~`) — `/api/%41%42C` becomes
  `/api/ABC` — while the WHATWG `URL` parser's `pathname` leaves it as given.
  `%2F` and `%20` are unaffected; only unreserved-set escapes differ.
- **A trailing `?` with an empty query.** `Uri.PathAndQuery` keeps the
  delimiter for `https://api.example.com/orders?`, but `URL.search` is `""`
  when the query is empty, so the TypeScript builder drops it. Some
  query-string builders emit a trailing `?` for an empty parameter map —
  avoid that shape if you sign from the browser.

To see the two strings side by side, turn `Trace` on for the `HmacManager`
category on both ends and compare event 1002 (signer) with event 1105
(verifier). The first differing segment is the cause — see
[diagnosing a mismatch](../../dotnet/logging/#diagnosing-a-signature-mismatch).

## Replacing it

A policy can build its own signing content instead. That is a deliberate
escape hatch with sharp edges — see
[custom signing content](../../dotnet/custom-signing-content/).
