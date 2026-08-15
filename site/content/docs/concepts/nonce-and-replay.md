---
title: Nonce and replay
description: How a captured request is stopped from being sent twice, and which cache to use.
weight: 4
---

A valid signature stays valid. Without something else in play, anyone who
captures a signed request can send it again and it will verify — the signature
is still correct, because nothing about it has changed.

Two things prevent that, and they work together:

**The timestamp.** Every signature covers `Hmac-DateRequested`. The verifier
rejects anything older than the policy's `maxAgeInSeconds`, which bounds how
long a captured request is useful at all.

**The nonce.** Every signature covers a `Guid` generated per request. The
verifier stores each nonce it accepts for the length of that same window and
rejects a repeat. So inside the window a request works exactly once, and
outside it the timestamp check has already rejected it.

Neither can be tampered with: both are in the signing content, so changing
either invalidates the signature.

## Choosing the window

```csharp
policy.UseMemoryCache(maxAgeInSeconds: 300);
```

`maxAgeInSeconds` is both the freshness window and how long a nonce is
remembered. It has to absorb the real clock skew between signer and verifier
plus network time — too tight and legitimate requests fail; too loose and a
captured request stays replayable for longer, and the cache holds more.

Minutes, not hours. The chart defaults to 60 seconds.

## Which cache

| | `UseMemoryCache` | `UseDistributedCache` |
| --- | --- | --- |
| Backed by | in-process memory | Redis |
| Shared between instances | no | yes |
| Safe for | a single instance | any number |

{{% hm-note kind="warn" %}}
The in-memory cache is per process. Behind a load balancer with two instances,
a replayed request that lands on the instance that has not seen the nonce is
accepted — so the protection quietly does nothing. Any multi-instance
deployment needs the distributed cache.
{{% /hm-note %}}

```csharp
policy.UseDistributedCache(maxAgeInSeconds: 300);
```

The distributed cache uses the registered `IDistributedCache`, so it is wired
the usual ASP.NET Core way:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = "localhost:6379");
```

If a policy asks for the distributed cache and no `IDistributedCache` is
registered, that is reported at `Warning` (event 1201) rather than failing
silently.

## In Kubernetes

The chart bundles Redis and every policy uses the distributed cache, so
multi-replica is the default and correct configuration. Setting
`redis.enabled=false` falls back to the in-process cache, and the chart then
refuses `replicaCount > 1` rather than letting you deploy something that does
not protect anything. See [Redis](../../kubernetes/redis/).
