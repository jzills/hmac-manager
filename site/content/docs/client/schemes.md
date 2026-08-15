---
title: Schemes
description: Folding header values into the signature, on both sides.
weight: 4
---

A [scheme](../../concepts/schemes/) names headers whose values become part of
the signature. On the client it is a name and a flat list of header names:

```ts
type HmacScheme = {
  name: string;
  headers: string[];
};
```

There is no `claimType` here. When this package verifies, it hands the covered
values back on the result and stops there — see
[below](#verifying-with-a-scheme). Mapping them onto a principal is the .NET
handler's job, and it needs a name to map onto.

```ts
const factory = new HmacManagerFactory([{
  name: "MyPolicy",
  publicKey: "00000000-0000-0000-0000-000000000001",
  privateKey: "zvg29s2cQ4idOqbUJWETOw==",
  contentHashAlgorithm: HashAlgorithm.SHA256,
  signatureHashAlgorithm: HashAlgorithm.SHA256,
  schemes: [{
    name: "UserScheme",
    headers: ["X-UserId", "X-Email"]
  }]
}]);
```

Select the scheme when creating the manager:

```ts
const manager = factory.create("MyPolicy", "UserScheme");
```

## The headers must already be on the request

The values are read off the request at signing time, so they have to be set
first:

```ts
const request = new Request("https://api.example.com/orders", {
  headers: {
    "X-UserId": "42",
    "X-Email": "someone@example.com"
  }
});

const result = await manager.sign(request);
```

{{% hm-note kind="warn" %}}
If any header the scheme names is missing, signing fails — and it fails
*quietly*: `sign` returns `isSuccess: false` rather than throwing, with the
reason on `result.error`. Adding a header after signing is just as wrong, since
it is then not covered by the signature and the verifier rejects the request.
Always check `isSuccess`.
{{% /hm-note %}}

## Order matters

The values are appended to the signing content in the order the scheme
declares its headers, not the order they appear on the request. Both sides must
declare them in the same order — `["X-UserId", "X-Email"]` and
`["X-Email", "X-UserId"]` are different schemes as far as the signature is
concerned, even though they name the same headers.

## Verifying with a scheme

Nothing extra to configure — the scheme is on the policy, and the request names
which one it used. On success the covered values come back by header name:

```ts
const result = await verifier.verify(request);

if (result.isSuccess) {
  const userId = result.headerValues!["X-UserId"];
}
```

These are the only header values worth trusting on the request. They are inside
the signature, so altering one in transit invalidates it; every other header
travelled unprotected.

A request missing a header its scheme names fails with `headers-missing` — the
signature covers a value that is not there, so there is nothing to compare. See
[verifying requests](../verifying-requests/).

## Server side

The matching .NET policy:

```csharp
policy.AddScheme("UserScheme", scheme =>
{
    scheme.AddHeader("X-UserId", ClaimTypes.NameIdentifier);
    scheme.AddHeader("X-Email", ClaimTypes.Email);
});
```

Or as an [`HmacPolicy` resource](../../kubernetes/hmacpolicy-crd/):

```yaml
schemes:
  - name: UserScheme
    headers:
      - name: X-UserId
        claimType: userId
      - name: X-Email
        claimType: email
```
