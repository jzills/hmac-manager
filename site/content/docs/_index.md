---
title: Documentation
linkTitle: Docs
description: Add HMAC authentication to an ASP.NET Core API, or enforce it across an Istio mesh.
weight: 1
---

HmacManager signs and verifies HTTP requests against named policies. A policy
carries a key pair, a hash algorithm choice, a replay window, and optionally a
set of schemes; a request names the policy it was signed with, and the verifier
recomputes the signature and compares.

The same model is available in three places, and they interoperate in every
direction — all three build the same signing content, and all three can sign or
verify. A request signed by the TypeScript client verifies against the .NET
handler or the mesh verifier, and one signed in .NET verifies in a Node service.
A [shared fixture](concepts/signing-content/#cross-implementation-parity) is
what keeps that true.

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300);
        });
    });
```

New here? [Getting started](getting-started/) installs one of the three and
gets a request signed and verified. [Concepts](concepts/) explains what a
policy, a scheme and the signing content actually are — worth reading once,
because the same vocabulary is used by all three.

{{< hm-children >}}
