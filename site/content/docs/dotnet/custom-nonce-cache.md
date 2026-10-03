---
title: Custom nonce cache
description: Backing replay protection with a store of your own, and the one operation it must provide.
weight: 8
---

The built-in caches cover one instance (`UseMemoryCache`) and any
`IDistributedCache` (`UseDistributedCache`). A custom cache is for a shared
store that can claim a nonce **atomically**, which `IDistributedCache` cannot —
see [nonce and replay](../../concepts/nonce-and-replay/#verification-order).

For Redis, that cache already exists: use the
[`HmacManager.StackExchangeRedis`](../redis-nonce-cache/) package. The Redis
example below shows the contract, not a replacement for it.

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
  cache serves every policy that selects it. Round a TTL up to your store's
  resolution: `RedisCache` truncates to whole seconds, so an entry written with
  0.8 seconds left is dropped at once;
- **return `false` rather than store an entry whose expiry has already passed**,
  since some stores throw on an expiry in the past.

The verifier checks the request's date again immediately before the call, so a
request that expired while its signature was computed never reaches the cache, and
it reports any `false` as a replay.

## Derive from `NonceCache`

`NonceCache` handles the last two rules: it refuses a request whose window has
closed, and hands you the time left in it as a TTL — positive, relative to now, and
rounded up to a whole second. What is left is the atomic claim:

```csharp
using HmacManager.Caching;
using StackExchange.Redis;

public class RedisNonceCache(IConnectionMultiplexer redis) : NonceCache
{
    protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) =>
        redis.GetDatabase().StringSetAsync(
            CreateKey(NonceCacheType.Distributed, nonce), 1, timeToLive, When.NotExists);
}
```

`false` from `TryAddCoreAsync` means the nonce was already claimed — nothing else.
The TTL is relative because a store reached over the network can see an absolute
expiry pass on the way. `Clock` is the `TimeProvider` passed to the base
constructor — `TimeProvider.System` unless you supply one, which is what makes the
expiry testable.

`CreateKey(NonceCacheType.Distributed, nonce)` is the key the built-in
distributed cache records a nonce under. Use it when your cache takes over a
store the built-in one has been writing to, as this one would in an application
that used `UseDistributedCache` over `AddStackExchangeRedisCache`: a nonce
recorded before the switch then stays claimed after it. Under a key of your own
it would be unclaimed again, and a request captured just before the switch could
be replayed just after it. `RedisCache` prefixes every key with its
`InstanceName`, if you set one; add the same prefix.

## Register it

Policies select a cache by type (`UseMemoryCache`, `UseDistributedCache`, or
`CacheType` in configuration). `AddNonceCache` puts yours behind one type, and
policies on the other keep the built-in cache:

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

builder.Services.AddNonceCache<RedisNonceCache>(NonceCacheType.Distributed);
```

The cache is a singleton, shared by every request, so it must be safe to call
concurrently. `AddNonceCache<T>` resolves `T` from the container if you
registered it there, and otherwise constructs it with its dependencies resolved
from the container. The other overload takes a factory:

```csharp
builder.Services.AddNonceCache(NonceCacheType.Distributed, services =>
    new RedisNonceCache(services.GetRequiredService<IConnectionMultiplexer>()));
```

It can come before or after `AddHmacManager`, and the last registration for a
type wins. The built-in cache it replaces is never constructed, so the store
behind it (the `IDistributedCache`, here) is never resolved.

{{% hm-note kind="warn" %}}
Registering an `IComponentCollection<INonceCache>` of your own after
`AddHmacManager` still works, but it replaces every type's cache, built-ins
included. A policy whose type is missing from it is not verified, and is
reported at `Warning` (event 1201).
{{% /hm-note %}}

Outside dependency injection, pass a `NonceCacheCollection` to
`HmacManagerFactory`, or a single cache to the `HmacManager` constructor.

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
