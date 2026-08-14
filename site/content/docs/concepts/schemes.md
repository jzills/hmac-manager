---
title: Schemes
description: Named header sets whose values are folded into the signature, and mapped to claims.
weight: 2
---

A scheme is a named list of headers whose **values become part of the
signature**. It answers a question a bare HMAC cannot: this request is
authentic, but authentic *as whom*?

Without a scheme, a signature covers the method, the URI, the timestamp, the
public key, the body hash and the nonce. Anything else in the request — an
`X-UserId` header, a tenant id — is unsigned, so a caller holding a valid key
could change it freely. A scheme closes that.

```csharp
options.AddPolicy("PaymentsApi", policy =>
{
    policy.UsePublicKey(publicKey);
    policy.UsePrivateKey(privateKey);
    policy.AddScheme("UserContext", scheme =>
    {
        scheme.AddHeader("X-UserId");
        scheme.AddHeader("X-Email");
    });
});
```

A request selects the scheme with the `Hmac-Scheme` header. The signer and the
verifier must name the same one, and both fold the header values in, in the
order the scheme declares them.

{{% hm-note kind="warn" %}}
Every header a scheme names must already be on the request **before** you sign
it. In .NET, signing a request that is missing one fails; in the TypeScript
client, `sign` returns `isSuccess: false` rather than throwing — with the cause
on `result.error` — so a missing header is silent unless you check the result.
{{% /hm-note %}}

## Headers become claims

On the .NET side, when a request authenticates, each scheme header is added to
the `ClaimsPrincipal`. By default the claim type is the header's own name; pass
a second argument to choose one:

```csharp
policy.AddScheme("UserContext", scheme =>
{
    scheme.AddHeader("X-UserId", ClaimTypes.NameIdentifier);
    scheme.AddHeader("X-Email", ClaimTypes.Email);
});
```

So a verified request arrives at your endpoint with a populated
`User.Identity`, and the values there are ones the signature covers.

## Several schemes on one policy

A policy can carry several. They are alternatives, not a set applied together —
a request picks exactly one, or none:

```csharp
policy.AddScheme("UserContext", scheme => { scheme.AddHeader("X-UserId"); });
policy.AddScheme("ServiceContext", scheme => { scheme.AddHeader("X-ServiceId"); });
```

Requiring a *specific* scheme on a specific endpoint is an authorization
concern — see [authorization](../../dotnet/authorization/).

## In the other two surfaces

In the [TypeScript client](../../client/schemes/), a scheme is
`{ name, headers: string[] }` — a flat list of header names, with no claim
mapping, because the client only signs.

In [Kubernetes](../../kubernetes/hmacpolicy-crd/), schemes are declared on the
`HmacPolicy` resource and do carry `claimType`, since the verifier maps them.
