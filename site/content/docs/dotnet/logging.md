---
title: Logging
description: Categories, levels, and how to find the cause of a signature mismatch.
weight: 8
---

HmacManager writes to `ILogger` and needs no configuration to do so — register
the library the usual way and every component picks up the host's logger.
Nothing is written when logging is not configured: each type falls back to
`NullLogger`, so the library never depends on a logging stack being present.

## Categories

| Category | What it records |
| --- | --- |
| `HmacManager.Components.HmacManager` | The outcome of every sign and verify, and the specific reason a verification was rejected. |
| `HmacManager.Components.HmacManagerFactory` | Policies and nonce caches that could not be resolved. |
| `HmacManager.Mvc.HmacAuthenticationHandler` | The authentication pipeline's view of a request. |
| `HmacManager.Mvc.HmacAuthenticationContextProvider` | Requests naming a policy this host does not have. |
| `HmacManager.Mvc.HmacDelegatingHandler` | Outgoing requests abandoned because they could not be signed. |
| `HmacManager.Mvc.Extensions.Internal.HmacPolicyCollectionReloader` | Policy sets loaded and reloaded from configuration. |

Everything is under the `HmacManager` prefix, so the whole library turns up or
down at once:

```json
{
  "Logging": {
    "LogLevel": {
      "HmacManager": "Debug"
    }
  }
}
```

## Levels

| Level | Used for |
| --- | --- |
| `Information` | The live policy set, at startup and whenever a configuration change replaces it. On by default. |
| `Warning` | A recognized signing attempt that was rejected — expired, replayed, or mismatched — and server-side faults such as a configuration change that could not be applied. |
| `Debug` | Per-request outcomes (signed, verified, skipped) and unrecognized requests — no HMAC header, unparseable headers, or an unknown policy name. |
| `Trace` | The signing content and both signatures behind a mismatch. |

The split between `Warning` and `Debug` is deliberate. Anything a caller can
trigger at will — a request with no HMAC header, an unknown policy name — is
`Debug`, so an edge deployment cannot be driven to unbounded `Warning` volume
by unauthenticated traffic.

## Events

Event ids are stable across releases. An id is never reused for a different
event. The full table is in the [event reference](../../reference/log-events/).

## Diagnosing a signature mismatch

A mismatch (event 1104) almost always means the two sides built different
[signing content](../../concepts/signing-content/): a relative request URI, a
proxy rewriting the host, or a request body read before signing.

Turn on `Trace` for the `HmacManager` category on **both** the signer and the
verifier, then compare the two signing content strings — event 1002 on the
signer, event 1105 on the verifier. The first differing segment is the cause.

```json
{
  "Logging": {
    "LogLevel": {
      "HmacManager.Components.HmacManager": "Trace"
    }
  }
}
```

{{% hm-note kind="warn" %}}
Private keys are never logged at any level, and a test asserts that over the
whole sign/verify path with every level enabled.

Signing content is not a secret — it travels with the request — but it does
contain the public key, the nonce and any scheme header values, which may
identify a user. `Trace` is off by default for that reason and is not intended
for production.
{{% /hm-note %}}

## In Kubernetes

The verifier and the operator use the same catalogue and the same levels,
controlled by one chart value:

```yaml
logging:
  level: Information
```

Framework and dependency logging (ASP.NET Core, KubeOps) is suppressed below
`Error` regardless, so routine chatter stays out while genuine framework errors
still surface. See [chart values](../../reference/chart-values/).
