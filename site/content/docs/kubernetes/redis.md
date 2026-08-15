---
title: Redis and replay protection
linkTitle: Redis
description: Why the chart bundles Redis, and the replica constraint when it does not.
weight: 5
---

Redis is deployed as part of the release when `redis.enabled=true`, which is
the default. Every policy then uses the distributed
[nonce cache](../../concepts/nonce-and-replay/) — no connection strings, no
external Redis cluster to provision.

| Value | Default | Meaning |
| --- | --- | --- |
| `redis.enabled` | `true` | Deploy bundled Redis and use the distributed nonce cache |

## Turning it off

```yaml
redis:
  enabled: false
```

Policies then use the in-process cache, which lives in one pod's memory.

The chart refuses `replicaCount > 1` in that configuration. That is not
conservatism: with two replicas and a per-process cache, a replayed request
that lands on the pod which has not seen the nonce is accepted. Replay
protection would appear configured and do nothing. Rather than let that deploy,
the chart fails.

So `redis.enabled=false` is for single-replica deployments only — a
development cluster, or something genuinely running one pod.

## What is stored

Only used nonces, each for the length of its policy's `maxAgeInSeconds`. No
keys, no request content, no signatures. A nonce is a `Guid` that is already
sent in the clear on every request, so the Redis instance holds nothing secret
— though an attacker who could *delete* from it could replay requests inside
the window, so it should not be world-writable.

Entries expire on their own; nothing needs pruning.

## Sizing the window

`nonce.maxAgeInSeconds` on each policy is both the freshness window and how
long each nonce is held. The chart defaults to 60 seconds. Longer windows
tolerate more clock skew and hold more entries; shorter ones reject legitimate
requests when clocks drift. See
[nonce and replay](../../concepts/nonce-and-replay/).
