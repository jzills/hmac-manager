---
title: Verifying requests
description: Authenticating incoming requests in a Node service with the hmac-manager package.
weight: 3
---

`verify` is the other half of [`sign`](../signing-requests/): it takes an
incoming request, recomputes the signature from the policy's keys, and reports
whether the two match. A Node service can therefore both call an
HmacManager-protected API and be one, without a .NET hop or a mesh sidecar.

It runs the same checks in the same order as the .NET library, and rejects the
same requests — the two are held to one wire format by a
[shared fixture](../../concepts/signing-content/#cross-implementation-parity).

{{% hm-note kind="warn" %}}
Verification needs the private key, so it belongs on a server. A browser bundle
that can verify is a browser bundle that has published your shared secret — see
[where the private key lives](../install/#where-the-private-key-lives).
{{% /hm-note %}}

## The shape of it

```ts
import { HmacManagerFactory, HashAlgorithm } from "hmac-manager";

const verifier = new HmacManagerFactory([{
  name: "MyPolicy",
  publicKey: "00000000-0000-0000-0000-000000000001",
  privateKey: process.env.HMAC_PRIVATE_KEY!,
  contentHashAlgorithm: HashAlgorithm.SHA256,
  signatureHashAlgorithm: HashAlgorithm.SHA256,
  maxAgeInSeconds: 30,
  schemes: []
}]);

const result = await verifier.verify(request);

if (!result.isSuccess) {
  return new Response(null, { status: 401 });
}
```

`verify` on the **factory** is what a server wants. A verifier does not know
which policy a caller used until it reads the request, so resolving the policy
is part of verifying rather than something to do beforehand. `verify` on a
**manager** exists too, for when the policy is already fixed.

Like `sign`, it never throws. Every way a request can be wrong is an outcome.

## Why it failed

`result.reason` says which check rejected the request:

| Reason | Meaning |
| --- | --- |
| `headers-missing` | A header needed to verify was not sent — including a header a scheme covers. |
| `headers-malformed` | A header was sent but is unusable: a non-`Hmac` `Authorization`, a nonce that is not a UUID, a date that is not an integer. |
| `policy-not-found` | The request names a policy, or a scheme within one, that is not registered. |
| `expired` | Outside the validity window — too old, or dated in the future. |
| `replayed` | This nonce has already been used. |
| `signature-mismatch` | The signature does not match the one computed here. |
| `verification-error` | Computing the signature threw. A fault on this side, not a rejected caller. |

{{% hm-note %}}
Log the reason; do not return it. The .NET library distinguishes these in its
logs and returns an undifferentiated failure over the wire, because a verifier
facing the open internet should not narrate why a forgery was rejected. A flat
401 is the right response, which is what the
[ext-authz service](../../kubernetes/ext-authz-service/) does.
{{% /hm-note %}}

Only `verification-error` is yours to fix. The rest are the caller's, and
`signature-mismatch` is usually a
[signing-content mismatch](../../concepts/signing-content/#things-that-break-a-match).

## Replay protection

Every accepted nonce is recorded so the same request cannot be sent twice
inside its window — the same mechanism as the
[.NET nonce cache](../../concepts/nonce-and-replay/). The default store is
in-process:

```ts
new HmacManagerFactory(policies);                    // MemoryNonceStore, 30s
```

{{% hm-note kind="warn" %}}
`MemoryNonceStore` is per-process. Behind a load balancer with two replicas, a
replayed request that lands on the instance that has not seen the nonce is
accepted, and the protection quietly does nothing. Any multi-replica deployment
needs a shared store.
{{% /hm-note %}}

Supply one by implementing `NonceStore`, which is two methods:

```ts
import Redis from "ioredis";
import { HmacManagerFactory } from "hmac-manager";
import type { NonceStore } from "hmac-manager";

const redis = new Redis(process.env.REDIS_URL!);

const store: NonceStore = {
  has: async nonce => (await redis.exists(`hmac:nonce:${nonce}`)) === 1,
  set: async (nonce, dateRequested) => {
    // Dated from when the request was signed, not from now: an entry that
    // outlives its signature's window guards nothing, because the request is
    // already rejected as expired before the nonce is consulted.
    const remaining = 30_000 - (Date.now() - dateRequested.getTime());
    if (remaining > 0) {
      await redis.set(`hmac:nonce:${nonce}`, "1", "PX", remaining);
    }
  }
};

const verifier = new HmacManagerFactory(policies, false, store);
```

The default `has`-then-`set` is not atomic, so two simultaneous replays of the
same request can both slip through. Closing that needs an atomic primitive from
the store — `SET … NX` on Redis, whose reply tells you whether you claimed the
key — so a `NonceStore` that can do better should. The .NET library has the same
gap in the same place.

## Node, Express and friends

Runtimes with a native `Request` — Hono, Next.js route handlers, Deno, Bun,
Cloudflare Workers — pass it straight to `verify`. Node's `http` module, and
everything built on it, hands you an `IncomingMessage` instead.
`fromNodeRequest` converts one:

```ts
import { fromNodeRequest } from "hmac-manager";

const result = await verifier.verify(fromNodeRequest(req, { body: rawBody }));
```

It closes two traps, both of which otherwise surface as a
`signature-mismatch` indistinguishable from a forgery.

### The body must be the raw bytes

The content hash is over exactly what arrived. Once `express.json()` has run,
the raw bytes are gone and `JSON.stringify(req.body)` is *not* byte-identical
to what the caller signed — key order, whitespace and number formatting are all
free to differ. Capture the bytes during parsing:

```ts
app.use(express.json({
  verify: (req, _res, buf) => { (req as any).rawBody = buf; }
}));

app.use(async (req, res, next) => {
  const result = await verifier.verify(
    fromNodeRequest(req, { body: (req as any).rawBody }));

  if (!result.isSuccess) {
    console.warn("hmac rejected", { reason: result.reason });
    return res.sendStatus(401);
  }

  res.locals.hmac = result.headerValues;
  next();
});
```

### The origin must be the one the caller signed

The signing content covers the host, so the URL has to be rebuilt as the caller
addressed it. By default `fromNodeRequest` uses the `Host` header and whether
the socket is TLS.

Behind an ingress or load balancer that terminates TLS, that gives `http` where
the caller signed `https`. Opt in to the forwarded headers:

```ts
fromNodeRequest(req, { body: rawBody, trustProxy: true });
```

{{% hm-note kind="warn" %}}
`trustProxy` is off by default on purpose. `x-forwarded-proto` and
`x-forwarded-host` are ordinary request headers, so anything that can reach the
process directly can set them, and the process cannot tell whether a proxy
overwrote them. Turn it on only where one is guaranteed to.
{{% /hm-note %}}

Where neither is right — a Unix socket, or a proxy that rewrites `Host` —
name the origin outright:

```ts
fromNodeRequest(req, { body: rawBody, baseUrl: "https://api.example.com" });
```

## Scheme header values become claims

A [scheme](../schemes/) folds named header values into the signature, so they
cannot be altered in transit. On success they come back by name:

```ts
const result = await verifier.verify(request);

if (result.isSuccess) {
  const tenantId = result.headerValues!["X-Tenant-Id"];
}
```

These are the caller's claims about themselves that they committed to when they
signed — the .NET handler turns exactly these into `Claim`s. A request missing
one of them fails with `headers-missing`, because the signature covers a value
that is not there.

## Both ends must agree

Two settings are shared configuration rather than negotiated, and a mismatch
rejects every request:

- **Consolidated headers.** The second constructor argument must match the
  signer's. A verifier expecting `Hmac-Options` against a signer sending the
  individual headers reports `headers-missing`. See
  [headers](../../concepts/headers/).
- **The algorithms.** Different hash algorithms produce different signatures,
  reported as `signature-mismatch`.

`maxAgeInSeconds` is not shared: each end applies its own, and the shorter one
decides. A verifier configured tighter than its signers rejects requests that
were never late.

## A runnable example

[`samples/NodeApiAuthentication`](https://github.com/jzills/hmac-manager/tree/main/samples/NodeApiAuthentication)
is a Node API doing all of the above, called by a Node client and a .NET client —
including the replay and tampering cases, so you can watch them be refused.
