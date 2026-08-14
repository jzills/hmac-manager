---
title: Events
description: Hooks in the authentication handler — key validation, success and failure.
weight: 5
---

`HmacEvents` exposes three points in the `HmacAuthenticationHandler` flow.

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy => { /* ... */ });

        options.Events = new HmacEvents
        {
            OnValidateKeysAsync = (context, keys) => { /* ... */ },
            OnAuthenticationSuccessAsync = (context, hmacResult) => { /* ... */ },
            OnAuthenticationFailureAsync = (context, hmacResult) => { /* ... */ }
        };
    });
```

| Event | Runs | Returns |
| --- | --- | --- |
| `OnValidateKeysAsync` | after a signature is parsed, before any verification | `Task<bool>` |
| `OnAuthenticationSuccessAsync` | after a signature verifies | `Task<Claim[]>` |
| `OnAuthenticationFailureAsync` | after a signature fails to verify | `Task<Exception>` |

The defaults pass through: `true`, an empty `Claim[]`, and an
`HmacAuthenticationException`. Setting none of them changes nothing.

## OnValidateKeysAsync

Runs before verification, with the keys the request named. Returning `false`
rejects the request without attempting to verify it — which is the cheap place
to reject a revoked or unknown public key, since it skips the hashing entirely.

```csharp
OnValidateKeysAsync = async (context, keys) =>
{
    var store = context.RequestServices.GetRequiredService<IKeyStore>();
    return await store.IsActiveAsync(keys.PublicKey);
}
```

A rejection here is logged at `Warning` as event 1301.

## OnAuthenticationSuccessAsync

Runs after a successful verification. The claims it returns are added to the
principal, on top of the ones a scheme already contributes — the place to
attach roles or tenant context that the signature does not carry.

```csharp
OnAuthenticationSuccessAsync = async (context, hmacResult) =>
{
    var accounts = context.RequestServices.GetRequiredService<IAccountService>();
    var account = await accounts.FindAsync(hmacResult.Hmac?.Policy);
    return [new Claim(ClaimTypes.Role, account.Role)];
}
```

## OnAuthenticationFailureAsync

Runs after a failed verification, and returns the exception that becomes the
authentication failure. Use it to record the attempt, or to return something
more specific than the default.

```csharp
OnAuthenticationFailureAsync = (context, hmacResult) =>
{
    // hmacResult carries the Hmac that was computed, where one could be
    Task.FromResult<Exception>(new HmacAuthenticationException("rejected"));
}
```

{{% hm-note kind="warn" %}}
Do not put the private key, or anything derived from it, into a claim or an
exception message. The library guarantees it never logs one; an exception
message you write is outside that guarantee and tends to end up in logs
anyway.
{{% /hm-note %}}

## With configuration binding

The `IConfigurationSection` overload has no options delegate, so it takes
events as a second argument:

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(configurationSection, new HmacEvents
    {
        OnValidateKeysAsync = (context, keys) => { /* ... */ }
    });
```
