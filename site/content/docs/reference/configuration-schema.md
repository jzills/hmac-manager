---
title: Configuration schema
description: The JSON shape an IConfigurationSection binds to.
weight: 4
---

The section passed to `AddHmac` or `AddHmacManager` binds to an **array** of
policies.

```json
[
  {
    "Name": "Some_Policy",
    "Keys": {
      "PublicKey": "37e3e675-370a-4ba9-af74-68f99b539f03",
      "PrivateKey": "zvg29s2cQ4idOqbUJWETOw=="
    },
    "Algorithms": {
      "ContentHashAlgorithm": "SHA256",
      "SigningHashAlgorithm": "HMACSHA256"
    },
    "Nonce": {
      "CacheType": "Memory",
      "MaxAgeInSeconds": 100
    },
    "Schemes": [
      {
        "Name": "Some_Scheme",
        "Headers": [
          {
            "Name": "Some_Header_1",
            "ClaimType": "Header_1_ClaimType"
          }
        ]
      }
    ]
  }
]
```

| Property | Values |
| --- | --- |
| `Name` | Policy name, matched against the `Hmac-Policy` header |
| `Keys.PublicKey` | A GUID string |
| `Keys.PrivateKey` | A base64-encoded string |
| `Algorithms.ContentHashAlgorithm` | `SHA1`, `SHA256`, `SHA512` |
| `Algorithms.SigningHashAlgorithm` | `HMACSHA1`, `HMACSHA256`, `HMACSHA512` |
| `Nonce.CacheType` | `Memory`, `Distributed` |
| `Nonce.MaxAgeInSeconds` | Replay window, in seconds |
| `Schemes[].Name` | Scheme name, matched against the `Hmac-Scheme` header |
| `Schemes[].Headers[].Name` | Header name |
| `Schemes[].Headers[].ClaimType` | Claim the header maps to; defaults to the header name |

Values are validated when the policy is built, so an invalid key or algorithm
fails at startup rather than on the first request.

`Distributed` requires an `IDistributedCache` to be registered. If none is,
that is reported at `Warning` as event 1201 rather than failing quietly — see
[nonce and replay](../../concepts/nonce-and-replay/).

{{% hm-note kind="warn" %}}
`PrivateKey` is a shared secret and `appsettings.json` is committed. Supply it
from user secrets, an environment variable or a secret store and let
configuration composition merge it in.
{{% /hm-note %}}

See [configuration binding](../../dotnet/configuration-binding/) for how to
wire it up, including reload behaviour.
