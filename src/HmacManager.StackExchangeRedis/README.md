# HmacManager.StackExchangeRedis

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.StackExchangeRedis.svg)](https://www.nuget.org/packages/HmacManager.StackExchangeRedis/) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml)

Atomic, Redis-backed replay protection for
[HmacManager](https://www.nuget.org/packages/HmacManager/).

The built-in `Distributed` nonce cache works through `IDistributedCache`, which
has no conditional write, so two copies of a request reaching two instances at
the same moment can both be accepted. This package claims each nonce with one
`SET NX` instead: exactly one copy is accepted, however many instances share the
Redis.

Targets `net8.0` and `net10.0`.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/dotnet/redis-nonce-cache/)**

## Install

```bash
dotnet add package HmacManager.StackExchangeRedis
```

## Use it

Policies on `UseDistributedCache` are protected by Redis; policies on
`UseMemoryCache` keep the in-process cache.

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

The options match `AddStackExchangeRedisCache`'s — `Configuration`,
`ConfigurationOptions`, `ConnectionMultiplexerFactory` and `InstanceName` — and
the keys match the built-in cache's, so nonces recorded before switching stay
claimed after it.

## Log events

| Id | Level | Event |
| --- | --- | --- |
| 1400 | `Warning` | A nonce claim failed: Redis unreachable, timed out or errored. The request fails. |
| 1401 | `Warning` | The Redis connection was lost |
| 1402 | `Information` | The Redis connection was restored |
