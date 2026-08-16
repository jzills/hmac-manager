---
title: API
description: The full exported surface of the hmac-manager package.
weight: 5
---

```ts
import {
  HmacManagerFactory,
  HmacManager,
  HashAlgorithm,
  HmacAuthenticationDefaults,
  MemoryNonceStore,
  MissingHeaderError,
  BadHeaderFormatError,
  fromNodeRequest
} from "hmac-manager";
```

## HmacManagerFactory

```ts
new HmacManagerFactory(
  policies: HmacPolicy[],
  isConsolidatedHeadersEnabled?: boolean,  // default false
  nonceStore?: NonceStore                  // default new MemoryNonceStore()
)
```

Holds the policy set. `isConsolidatedHeadersEnabled` collapses the four
`Hmac-*` headers into a single `Hmac-Options` header and must match the other
end — see [headers](../../concepts/headers/).

`nonceStore` is used only when verifying. The default is per-process; supply a
shared one for any multi-replica deployment — see
[replay protection](../verifying-requests/#replay-protection).

```ts
create(policy: string, scheme?: string | null): HmacManager | null
```

Returns a manager, or `null` when a name was given and does not resolve — an
unregistered policy, or a scheme that policy does not declare. It does not throw.

**Blank means no scheme, not a scheme named blank.** `null`, `undefined`, `""`
and whitespace are all "I am not using a scheme", so a value read from
configuration or a form works whichever way absence reaches you. Only an
all-blank string counts; a real name is not trimmed, so `" UserScheme "` is a
different name from `"UserScheme"` and does not match.

```ts
factory.create("MyPolicy");                // a manager, no scheme
factory.create("MyPolicy", null);          // the same
factory.create("MyPolicy", "");            // the same
factory.create("MyPolicy", "   ");         // the same
factory.create("MyPolicy", "UserScheme");  // a manager using that scheme
factory.create("MyPolicy", "Typo");        // null
factory.create("MyPolicy", " UserScheme ");// null — not trimmed
factory.create("Nope");                    // null
```

This matches the .NET factory, which decides the same question with
`IsNullOrWhiteSpace`, so the two agree on every row above.

{{% hm-note %}}
A `null` for a scheme name you expected to work is the check doing its job. It
used to return a working manager that signed **without** the scheme, which the
verifier then rejected as a signature mismatch — a symptom that looks nothing
like a misspelled scheme name.
{{% /hm-note %}}

```ts
verify(request: Request): Promise<HmacVerificationResult>
```

Reads the policy and scheme the request names, resolves the manager, and
verifies against it. The entry point a server wants, since a verifier does not
know which policy a caller used until it reads the request. Never throws; an
unregistered policy or scheme comes back as `reason: "policy-not-found"`.

## HmacManager

```ts
sign(request: Request): Promise<HmacResult>
```

Adds the HMAC headers to `request` in place and returns the result. Never
throws — failures come back as `isSuccess: false`, with the cause on
`result.error`.

```ts
verify(request: Request): Promise<HmacVerificationResult>
```

Verifies against this manager's policy and scheme specifically. The request is
not modified, and its body stays readable afterwards — it is hashed through a
clone. A request naming a different policy or scheme is rejected rather than
verified against this one.

Prefer `HmacManagerFactory.verify` unless the policy is already fixed: a manager
constructed directly gets a nonce store of its own, and one that only ever sees
its own requests detects no replays.

## HmacPolicy

```ts
type HmacPolicy = {
  name: string;
  publicKey: string;                  // GUID, any case
  privateKey: string;                 // base64
  contentHashAlgorithm: HashAlgorithm;
  signatureHashAlgorithm: HashAlgorithm;
  schemes: HmacScheme[];
  maxAgeInSeconds?: number;           // default 30, verification only
  signingContentAccessor?: SigningContentAccessor;
};
```

`publicKey` may be given in any case. It is a GUID, and a GUID's wire form is the
canonical lowercase one — .NET gets that for free by holding the key as a `Guid`
— so a key configured in uppercase is lowercased before it reaches the
[signing content](../../concepts/signing-content/). Anything that is not a
canonical GUID is used exactly as configured, since `Guid.Parse` rejects it on
the .NET side and there is no rendering of it to agree with.

`maxAgeInSeconds` is the window a signature stays valid for when verifying;
signing ignores it. It matches `Nonce.MaxAgeInSeconds` on the .NET side. The two
ends do not have to agree, but the shorter of them decides.

`signingContentAccessor` replaces the default signing string, and matches
[`UseSigningContentBuilder`](../../dotnet/custom-signing-content/) on the .NET
side. Both ends must produce byte-identical strings.

## HmacScheme

```ts
type HmacScheme = {
  name: string;
  headers: string[];
};
```

## HmacResult

```ts
type HmacResult = {
  hmac: Hmac | null;
  isSuccess: boolean;
  dateGenerated: Date;
  error?: unknown;
};
```

`error` is present only on a failure and carries whatever was thrown — a missing
scheme header, a private key that is not valid base64, `crypto.subtle` being
unavailable outside a secure context.

It is typed `unknown` rather than `Error` because a `catch` binding is: anything
can be thrown in JavaScript. Narrow it before using it.

```ts
if (!result.isSuccess) {
  const reason = result.error instanceof Error
    ? result.error.message
    : String(result.error);
  throw new Error(`could not sign the request: ${reason}`);
}
```

## HmacVerificationResult

```ts
type HmacVerificationResult = {
  isSuccess: boolean;
  hmac: Hmac | null;
  dateGenerated: Date;
  reason?: HmacVerificationFailureReason;
  error?: unknown;
  headerValues?: Record<string, string>;
};

type HmacVerificationFailureReason =
  | "headers-missing"
  | "headers-malformed"
  | "policy-not-found"
  | "expired"
  | "replayed"
  | "signature-mismatch"
  | "verification-error";
```

A separate type from `HmacResult`, because the two carry different things: a
signing result's `hmac` is what was produced, a verification result's is what
the verifier recomputed and found to match. `hmac` is always `null` on failure —
the caller's unverified claims are not something to hand onward.

`reason` says which check rejected the request; see
[why it failed](../verifying-requests/#why-it-failed). `error` is present only
where a check threw. `headerValues` carries the scheme header values the
signature covered, by name, and is present only on success.

## NonceStore

```ts
interface NonceStore {
  has(nonce: string): Promise<boolean>;
  set(nonce: string, dateRequested: Date): Promise<void>;
}
```

Where used nonces are recorded. `MemoryNonceStore` implements it in-process:

```ts
new MemoryNonceStore(maxAgeInSeconds?: number)   // default 30
```

See [replay protection](../verifying-requests/#replay-protection) for a Redis
implementation and why a multi-replica deployment needs one.

## fromNodeRequest

```ts
fromNodeRequest(
  request: NodeRequestLike,
  options?: {
    body?: Uint8Array | string;
    baseUrl?: string;
    trustProxy?: boolean;   // default false
  }
): Request
```

Builds a Fetch `Request` from a Node `IncomingMessage`, for Express, Koa,
Fastify or raw `node:http`. Runtimes with a native `Request` do not need it.

Pass `body` for any request that has one — the content hash is over the raw
bytes, and a re-serialised parsed body does not reproduce them. See
[Node, Express and friends](../verifying-requests/#node-express-and-friends).

## Hmac

```ts
type Hmac = {
  policy: string;
  scheme: string | null;
  dateRequested: Date;
  nonce: string;
  signingContent: string;
  signature: string;
  signedHeaders: string[] | null;
};
```

`signingContent` is the exact string that was hashed — the thing to compare
against the server's when a signature does not match.

## HashAlgorithm

```ts
enum HashAlgorithm {
  SHA1   = "sha-1",
  SHA256 = "sha-256",
  SHA512 = "sha-512"
}
```

One enum for both the content hash and the signature hash. The .NET side
spells the signing one `HMACSHA256`; `HashAlgorithm.SHA256` in both positions
is the equivalent of `SHA256` plus `HMACSHA256`.

## HmacAuthenticationDefaults

```ts
HmacAuthenticationDefaults.AuthenticationScheme    // "Hmac"

HmacAuthenticationDefaults.Headers.Authorization   // "Authorization"
HmacAuthenticationDefaults.Headers.Policy          // "Hmac-Policy"
HmacAuthenticationDefaults.Headers.Scheme          // "Hmac-Scheme"
HmacAuthenticationDefaults.Headers.Nonce           // "Hmac-Nonce"
HmacAuthenticationDefaults.Headers.DateRequested   // "Hmac-DateRequested"
HmacAuthenticationDefaults.Headers.Options         // "Hmac-Options"
```
