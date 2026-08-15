---
title: Configuration binding
description: Declaring policies in appsettings.json instead of code.
weight: 3
---

Both `AddHmacManager` and `AddHmac` take an `IConfigurationSection` instead of
a builder delegate:

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(builder.Configuration.GetSection("HmacPolicies"));
```

## The schema

The section binds to an **array** of policies:

```json
{
  "HmacPolicies": [
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
}
```

Restricted values:

| Property | Values |
| --- | --- |
| `PublicKey` | a GUID string |
| `PrivateKey` | a base64-encoded string |
| `ContentHashAlgorithm` | `SHA1`, `SHA256`, `SHA512` |
| `SigningHashAlgorithm` | `HMACSHA1`, `HMACSHA256`, `HMACSHA512` |
| `CacheType` | `Memory`, `Distributed` |

Invalid values fail when the policy is built, not on the first request.

See the [full schema reference](../../reference/configuration-schema/).

{{% hm-note kind="warn" %}}
`PrivateKey` is a shared secret. `appsettings.json` is committed; put the key
in user secrets, an environment variable or a real secret store, and let
configuration composition supply it. The
[Kubernetes verifier](../../kubernetes/hmacpolicy-crd/) sources keys from
Secrets for exactly this reason.
{{% /hm-note %}}

## Reloading

Configuration providers that support reload (the JSON provider does, with
`reloadOnChange`) propagate changes to the live policy set without a restart.
The reloader reports the policy set it loaded at startup and on every change at
`Information` (events 1210 and 1211); a reload that fails keeps the previous
set and says so at `Warning` (event 1212). See [logging](../logging/).

## With events

The `IConfigurationSection` overload of `AddHmac` takes `HmacEvents` as an
optional second argument, since there is no options delegate to set them in:

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(configurationSection, new HmacEvents
    {
        OnValidateKeysAsync = (context, keys) => { /* ... */ },
    });
```

See [events](../events/).
