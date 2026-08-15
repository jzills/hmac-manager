---
title: Algorithms
description: The two hashes in play — one for the body, one for the signature.
weight: 5
---

Two separate hashes, chosen independently.

**The content hash** hashes the request body. Its output becomes one segment of
the [signing content](../signing-content/), which is how the body is covered by
the signature without being hashed twice.

**The signing hash** is the keyed HMAC over the finished signing content, using
the policy's private key. Its output is the signature in the `Authorization`
header.

| | Values | Default |
| --- | --- | --- |
| Content hash | `SHA1`, `SHA256`, `SHA512` | `SHA256` |
| Signing hash | `HMACSHA1`, `HMACSHA256`, `HMACSHA512` | `HMACSHA256` |

```csharp
policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA256);
policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA256);
```

Both sides must choose the same pair. There is no negotiation and no algorithm
identifier on the wire, so a disagreement surfaces as a signature mismatch on
every request rather than as a configuration error.

## Which to pick

`SHA256` / `HMACSHA256` unless you have a reason. It is the default on all
three surfaces.

`SHA512` costs a little more per request and buys little here — the signature
is short-lived by design, bounded by the [replay window](../nonce-and-replay/).

`SHA1` and `HMACSHA1` exist for talking to something that already speaks them.
SHA-1 is broken for collision resistance; HMAC-SHA1 is not broken in the same
way, but neither is a choice to make for something new.

## Elsewhere

In the TypeScript client the same choice is one enum for both, with values
`sha-1`, `sha-256` and `sha-512`:

```ts
contentHashAlgorithm: HashAlgorithm.SHA256,
signatureHashAlgorithm: HashAlgorithm.SHA256,
```

In Kubernetes they are `algorithms.contentHash` and `algorithms.signingHash` on
the [`HmacPolicy` resource](../../kubernetes/hmacpolicy-crd/), spelled the same
way as in .NET.
