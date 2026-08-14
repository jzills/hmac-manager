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

Returns a manager for that policy, or `null` when no policy of that name is
registered. It does not throw.

{{% hm-note kind="warn" %}}
Only the *policy* name is checked. Naming a scheme the policy does not declare
returns a working manager that signs **without** that scheme — no `null`, no
error. The signature then omits the header values the verifier expects to find,
and every request is rejected for a reason that looks nothing like a typo in a
scheme name. Check the spelling against the policy you registered.
{{% /hm-note %}}

## HmacManager

```ts
sign(request: Request): Promise<HmacResult>
```

Adds the HMAC headers to `request` in place and returns the result. Never
throws — failures come back as `isSuccess: false`.

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
};
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
