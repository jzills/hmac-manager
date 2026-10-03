---
title: Redis nonce cache
description: Atomic replay protection across instances, from the HmacManager.StackExchangeRedis package.
weight: 9
---

The built-in `Distributed` cache works through `IDistributedCache`, which has
no conditional write. It checks for a nonce and then records it, so two copies
of a request reaching two instances at the same moment can both be accepted —
see [nonce and replay](../../concepts/nonce-and-replay/#verification-order).

`HmacManager.StackExchangeRedis` claims each nonce with one Redis `SET NX`
instead. However many instances share the Redis, exactly one copy of a request
is accepted.

```bash
dotnet add package HmacManager.StackExchangeRedis
```

## Register it

```csharp
builder.Services.AddHmacManager(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.UsePublicKey(publicKey);
        policy.UsePrivateKey(privateKey);
        policy.UseDistributedCache(maxAgeInSeconds: 60);
    });
});

builder.Services.AddStackExchangeRedisNonceCache(options =>
    options.Configuration = "localhost:6379");
```

Every policy on `UseDistributedCache` (or `CacheType: Distributed` in
configuration) is then protected by Redis, and policies on `UseMemoryCache`
keep the in-process cache. Policies do not change. It can come before or after
`AddHmacManager`, and no `IDistributedCache` needs to be registered for it.

It is built on [`AddNonceCache`](../custom-nonce-cache/#register-it), the same
extension point a custom cache uses.

## Connecting

The options have the same names as `AddStackExchangeRedisCache`'s, so settings
copy across:

| Option | Purpose |
| --- | --- |
| `Configuration` | A StackExchange.Redis configuration string, such as `localhost:6379,password=…` |
| `ConfigurationOptions` | A `ConfigurationOptions`, used as given in place of `Configuration` |
| `ConnectionMultiplexerFactory` | Supplies the connection, in place of either. The cache does not dispose it |
| `InstanceName` | A prefix for every key — see [switching from `UseDistributedCache`](#switching-from-usedistributedcache) |

One of the first three must be set, or the host fails to start with an
`OptionsValidationException`.

The connection is opened on the first claim, asynchronously, so no thread
waits on it and a Redis that is slow to start does not hold up the host. A
configuration string connects with `abortConnect=false` unless it sets
`abortConnect` itself, so a Redis that is not up yet is retried in the
background rather than failing every claim until the process restarts.

To share a connection the application already has:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddStackExchangeRedisNonceCache(_ => { });

builder.Services.AddOptions<RedisNonceCacheOptions>()
    .Configure<IConnectionMultiplexer>((options, redis) =>
        options.ConnectionMultiplexerFactory = () => Task.FromResult(redis));
```

## When Redis is unavailable

A claim that Redis cannot answer throws, so the request fails instead of being
accepted without replay protection. Behind `AddHmac` that is an unhandled
exception from authentication, and the host's response is a `500`. Each failure
is logged at `Warning` as event 1400.

While the connection is down, StackExchange.Redis holds a claim in its backlog
for up to `syncTimeout` (5 seconds by default) in case the connection comes
back, and fails it after that. To fail at once instead, set
`BacklogPolicy = BacklogPolicy.FailFast` on the `ConfigurationOptions`.

The connection dropping and coming back are logged too, as events 1401 and
1402.

| Id | Level | Event |
| --- | --- | --- |
| 1400 | `Warning` | A nonce claim failed. The request fails |
| 1401 | `Warning` | The Redis connection was lost |
| 1402 | `Information` | The Redis connection was restored |

None of these can be caused by a caller at will: a nonce is only claimed for a
request whose signature has already been verified. No message includes the
connection configuration, which can hold a password.

## Switching from `UseDistributedCache`

The keys are the ones the built-in distributed cache writes. If an application
was using `UseDistributedCache` over `AddStackExchangeRedisCache`, set
`InstanceName` to the `RedisCache`'s own `InstanceName`, if it had one, and a
nonce recorded before the switch stays claimed after it.

During a rolling switch, an instance still on the built-in cache reads every
key as a hash, and Redis refuses that read with `WRONGTYPE` for a key this
cache wrote. That only
happens for a copy of a request another instance has already accepted, and the
copy is still not accepted — it fails as an error instead of being rejected.
Two instances both still on the built-in cache can both accept concurrent
copies, as before the switch; the one-copy guarantee holds once every instance
is on this cache.

## Outside dependency injection

```csharp
await using var cache = new RedisNonceCache(Options.Create(
    new RedisNonceCacheOptions { Configuration = "localhost:6379" }));
```

Pass it to the `HmacManager` constructor, or put it in a `NonceCacheCollection`
for `HmacManagerFactory`. Dispose it to close the connection it opened.

`RedisNonceCache` implements `TryAddAsync`, which is all the library calls.
The obsolete `SetAsync` and `ContainsAsync` throw `NotSupportedException`.
