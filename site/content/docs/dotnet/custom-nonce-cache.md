---
title: Custom nonce cache
description: Backing replay protection with a store of your own, and the one operation it must provide.
weight: 8
---

The built-in caches cover one instance (`UseMemoryCache`) and any
`IDistributedCache` (`UseDistributedCache`). A custom cache is for a shared
store that can claim a nonce **atomically**, which `IDistributedCache` cannot —
see [nonce and replay](../../concepts/nonce-and-replay/#verification-order).

## The contract

The verifier asks a cache one thing, through `INonceCache.TryAddAsync`: claim
this nonce until its request's window closes, and say whether it was unused.

```csharp
Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge);
```

An implementation must:

- **check and record in one atomic step**, so two concurrent copies of a request
  cannot both be accepted;
- **keep the entry until at least `dateRequested + maxAge`** — the signature stays
  valid until then, and `maxAge` is the policy's, passed on every call because one
  cache serves every policy that selects it;
- **return `false` rather than store an entry whose expiry has already passed.** A
  request can expire while its signature is computed, and some stores throw on an
  expiry in the past. The verifier reports that refusal as an expiry, not a replay.

## Derive from `NonceCache`

`NonceCache` handles the last two rules: it computes the expiry and refuses one
that has passed. What is left is the atomic claim:

```csharp
using HmacManager.Caching;
using StackExchange.Redis;

public class RedisNonceCache(IConnectionMultiplexer redis) : NonceCache
{
    protected override async Task<bool> TryAddCoreAsync(Guid nonce, DateTimeOffset expiresAt)
    {
        // A TTL relative to now, not the absolute expiry: the store's clock decides, so
        // a claim made milliseconds before expiry can never be rejected as "in the past".
        var remaining = expiresAt - Clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        return await redis.GetDatabase().StringSetAsync(
            $"hmac:nonce:{nonce}", 1, remaining, When.NotExists);
    }
}
```

`expiresAt` is in the future when `TryAddCoreAsync` is called, but a store reached
over the network can still see it pass, so treat a remainder that has run out as
a failed claim. `Clock` is the `TimeProvider` passed to the base constructor —
`TimeProvider.System` unless you supply one, which is what makes the expiry
testable.

## Register it

Policies select a cache by type (`UseMemoryCache`, `UseDistributedCache`, or
`CacheType` in configuration). Register a cache collection **after**
`AddHmacManager` to put yours behind the type your policies select:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddHmacManager(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.UsePublicKey(publicKey);
        policy.UsePrivateKey(privateKey);
        policy.UseDistributedCache(maxAgeInSeconds: 60);
    });
});

builder.Services.AddScoped<IComponentCollection<INonceCache>>(services =>
{
    var caches = new NonceCacheCollection();
    caches.Add(NonceCacheType.Distributed,
        new RedisNonceCache(services.GetRequiredService<IConnectionMultiplexer>()));
    return caches;
});
```

{{% hm-note kind="warn" %}}
This replaces the whole collection, built-in caches included. Add a cache for
every type your policies select: a policy whose type is missing is not verified,
and is reported at `Warning` (event 1201).
{{% /hm-note %}}

Outside dependency injection, pass the collection to `HmacManagerFactory`, or a
single cache to the `HmacManager` constructor.

## Migrating from `SetAsync` and `ContainsAsync`

Before `TryAddAsync`, a cache implemented `SetAsync` and `ContainsAsync`. Both are
obsolete and will be removed in the next major version. A cache that still
implements only those keeps working: the default `TryAddAsync` refuses an
expired entry, then calls `ContainsAsync` and `SetAsync`. That is check-then-set,
so it is not atomic, and the two-argument `SetAsync` never learns the policy's
max age — your cache's own TTL applies, and if it is shorter than the policy's
window a replay can get through after the entry expires.

To migrate, derive from `NonceCache` and move the logic into `TryAddCoreAsync`, or
implement `TryAddAsync` directly against the contract above.
