---
title: Policies
description: The unit of configuration — keys, algorithms, a replay window and schemes.
weight: 1
---

A policy is the top-level unit of configuration. It carries everything needed
to sign or verify a request:

| Part | What it is |
| --- | --- |
| Public key | A `Guid` identifying which key pair to verify against. Not a secret — it travels in the signing content. |
| Private key | A base64-encoded shared secret. Never transmitted, never logged. |
| Algorithms | One hash for the body, one for the signature. See [algorithms](../algorithms/). |
| Nonce | The replay window and which cache holds used nonces. See [nonce and replay](../nonce-and-replay/). |
| Schemes | Optional named header sets folded into the signature. See [schemes](../schemes/). |

A request names its policy in the `Hmac-Policy` header, so a verifier can hold
several and pick the right one per request. That is the point of naming them:
different callers can hold different private keys, and revoking one caller
means removing one policy.

```csharp
options.AddPolicy("PaymentsApi", policy =>
{
    policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
    policy.UseContentHashAlgorithm(ContentHashAlgorithm.SHA256);
    policy.UseSigningHashAlgorithm(SigningHashAlgorithm.HMACSHA256);
    policy.UseDistributedCache(maxAgeInSeconds: 300);
});
```

Both sides must agree on every part. A policy is not negotiated — if the signer
uses `HMACSHA512` and the verifier expects `HMACSHA256`, the signatures simply
do not match, and the rejection reports a mismatch rather than a configuration
problem. See [diagnosing a mismatch](../../dotnet/logging/#diagnosing-a-signature-mismatch).

## Key requirements

| Value | Must be |
| --- | --- |
| Public key | A GUID string |
| Private key | A base64-encoded string |

Both are validated when the policy is built, so a malformed key fails at
startup rather than on the first request.

## Where policies come from

They do not have to be hardcoded:

- **Static** — declared in code, or bound from an `IConfigurationSection`. The
  collection is a singleton. See [registration](../../dotnet/registration/) and
  [configuration binding](../../dotnet/configuration-binding/).
- **Reloaded** — bound from configuration and re-read when that configuration
  changes, with no restart.
- **Per request** — resolved from a database or any other store on each
  request. See [dynamic policies](../../dotnet/dynamic-policies/).
- **Kubernetes resources** — an `HmacPolicy` custom resource that an operator
  reconciles into the config the verifier mounts. See
  [the CRD](../../kubernetes/hmacpolicy-crd/).
