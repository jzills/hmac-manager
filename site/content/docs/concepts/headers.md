---
title: Headers
description: The headers a signed request carries, and how to consolidate them into one.
weight: 6
---

Signing adds these to the request. Everything except `Authorization` is
metadata the verifier needs in order to rebuild the
[signing content](../signing-content/).

| Header | Value | Present |
| --- | --- | --- |
| `Authorization` | `Hmac <signature>` | always |
| `Hmac-Policy` | the policy name | always |
| `Hmac-Scheme` | the scheme name | only when a scheme is used |
| `Hmac-Nonce` | a `Guid` | always |
| `Hmac-DateRequested` | Unix time in milliseconds | always |

```http
GET /orders HTTP/1.1
Host: api.example.com
Authorization: Hmac k3F9v0mQ8s2bQ1n0Xc7yA5tR6uW4pL8jH2gK1dS0fE=
Hmac-Policy: PaymentsApi
Hmac-Nonce: 6f9619ff-8b86-d011-b42d-00c04fc964ff
Hmac-DateRequested: 1723651200000
```

Note that the nonce and the timestamp are sent in the clear *and* covered by
the signature. They are not secrets — they are there so the verifier can
reproduce the content, and signing them is what stops anyone editing them.

`Hmac-Nonce` is accepted in either case, and is lowercased before it goes into
the [signing content](../signing-content/) — so a signer emitting an uppercase
GUID is verified the same by both implementations rather than by only one.

The names are constants, not strings to retype:

```csharp
HmacAuthenticationDefaults.AuthenticationScheme        // "Hmac"
HmacAuthenticationDefaults.Headers.Policy              // "Hmac-Policy"
HmacAuthenticationDefaults.Headers.DateRequested       // "Hmac-DateRequested"
```

```ts
import { HmacAuthenticationDefaults } from "hmac-manager";
HmacAuthenticationDefaults.Headers.Policy;             // "Hmac-Policy"
```

## Consolidated headers

The four `Hmac-*` headers can be collapsed into one, `Hmac-Options`, whose
value is a base64-encoded concatenation of the same fields.

```csharp
builder.Services.AddHmacManager(options =>
{
    options.EnableConsolidatedHeaders();
});
```

Useful where a proxy strips unknown headers, or where a header budget is
tight — four custom headers become one.

{{% hm-note kind="warn" %}}
This has to be enabled on **both** sides. A signer that consolidates and a
verifier that does not will fail on every request: the verifier looks for
`Hmac-Policy`, finds nothing, and treats the request as unsigned rather than as
invalid.
{{% /hm-note %}}

The TypeScript client takes it as the second constructor argument:

```ts
const factory = new HmacManagerFactory(policies, true);
```

See the [header reference](../../reference/headers/) for the exact values.
