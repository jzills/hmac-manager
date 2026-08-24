---
title: Headers
description: Every header HmacManager sends or reads, with types.
weight: 1
---

| Header | Value | Type | Present |
| --- | --- | --- | --- |
| `Authorization` | `Hmac <signature>` | base64 string | always |
| `Hmac-Policy` | Name of the policy | string | always |
| `Hmac-Scheme` | Name of the scheme | string | only with a scheme |
| `Hmac-Nonce` | Per-request nonce | GUID, 8-4-4-4-12, any case | always |
| `Hmac-DateRequested` | When the request was signed | Unix time, **milliseconds** | always |
| `Hmac-Options` | The four `Hmac-*` values, concatenated | base64 string | only with consolidated headers |

With [consolidated headers](../../concepts/headers/#consolidated-headers)
enabled, `Hmac-Options` replaces `Hmac-Policy`, `Hmac-Scheme`, `Hmac-Nonce` and
`Hmac-DateRequested`. `Authorization` is unaffected.

## Constants

Rather than retyping the strings:

```csharp
HmacAuthenticationDefaults.AuthenticationScheme          // "Hmac"
HmacAuthenticationDefaults.Headers.Authorization         // "Authorization"
HmacAuthenticationDefaults.Headers.Policy                // "Hmac-Policy"
HmacAuthenticationDefaults.Headers.Scheme                // "Hmac-Scheme"
HmacAuthenticationDefaults.Headers.Nonce                 // "Hmac-Nonce"
HmacAuthenticationDefaults.Headers.DateRequested         // "Hmac-DateRequested"
HmacAuthenticationDefaults.Headers.Options               // "Hmac-Options"
```

```ts
import { HmacAuthenticationDefaults } from "hmac-manager";
HmacAuthenticationDefaults.Headers.Policy;               // "Hmac-Policy"
```

## Forwarding through Istio

The ext-authz provider must forward these to the verifier, which is what
`includeRequestHeadersInCheck` does:

```yaml
includeRequestHeadersInCheck:
  - authorization
  - hmac-policy
  - hmac-nonce
  - hmac-daterequested
```

Names there are lowercase. Add `hmac-scheme` if your policies use schemes, and
`hmac-options` if you enable consolidated headers — a header that is not
forwarded is a header the verifier cannot see, and the request fails as if it
were unsigned. See [enforcement](../../kubernetes/enforcement/).

## An example request

```http
POST /orders HTTP/1.1
Host: api.example.com
Content-Type: application/json
Authorization: Hmac k3F9v0mQ8s2bQ1n0Xc7yA5tR6uW4pL8jH2gK1dS0fE=
Hmac-Policy: PaymentsApi
Hmac-Scheme: UserScheme
Hmac-Nonce: 6f9619ff-8b86-d011-b42d-00c04fc964ff
Hmac-DateRequested: 1723651200000
X-UserId: 42

{"sku":"ABC-1","quantity":2}
```
