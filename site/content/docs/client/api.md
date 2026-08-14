---
title: API
description: The full exported surface of the hmac-manager package.
weight: 4
---

```ts
import {
  HmacManagerFactory,
  HmacManager,
  HashAlgorithm,
  HmacAuthenticationDefaults
} from "hmac-manager";
```

## HmacManagerFactory

```ts
new HmacManagerFactory(
  policies: HmacPolicy[],
  isConsolidatedHeadersEnabled?: boolean   // default false
)
```

Holds the policy set. `isConsolidatedHeadersEnabled` collapses the four
`Hmac-*` headers into a single `Hmac-Options` header and must match the
verifier — see [headers](../../concepts/headers/).

```ts
create(policy: string, scheme?: string | null): HmacManager | null
```

Returns a manager, or `null` when either name does not resolve — an unregistered
policy, or a scheme that policy does not declare. It does not throw.

Passing no scheme is not a failure: it signs without one, which is what a policy
with no schemes wants.

```ts
factory.create("MyPolicy");              // a manager, no scheme
factory.create("MyPolicy", "UserScheme"); // a manager using that scheme
factory.create("MyPolicy", "Typo");      // null
factory.create("Nope");                  // null
```

{{% hm-note %}}
A `null` for a scheme name you expected to work is the check doing its job. It
used to return a working manager that signed **without** the scheme, which the
verifier then rejected as a signature mismatch — a symptom that looks nothing
like a misspelled scheme name.
{{% /hm-note %}}

## HmacManager

```ts
sign(request: Request): Promise<HmacResult>
```

Adds the HMAC headers to `request` in place and returns the result. Never
throws — failures come back as `isSuccess: false`, with the cause on
`result.error`.

## HmacPolicy

```ts
type HmacPolicy = {
  name: string;
  publicKey: string;
  privateKey: string;                 // base64
  contentHashAlgorithm: HashAlgorithm;
  signatureHashAlgorithm: HashAlgorithm;
  schemes: HmacScheme[];
  signingContentAccessor?: SigningContentAccessor;
};
```

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
