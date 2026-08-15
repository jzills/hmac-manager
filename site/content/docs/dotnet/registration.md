---
title: Registration
description: AddHmacManager versus AddHmac, and which one you want.
weight: 1
---

There are two entry points. They differ in one thing: whether verification is
wired into the ASP.NET Core authentication pipeline for you.

| | `AddHmacManager` | `AddHmac` |
| --- | --- | --- |
| Registers | `IHmacManagerFactory` | the same, plus an authentication handler |
| Verification | you call it | happens in the pipeline |
| Claims from schemes | you map them | mapped automatically |
| Use when | signing only, or verifying somewhere unusual | verifying an API |

Both accept the same policy configuration, so moving between them is not a
rewrite.

## AddHmac — the usual case

Registers `HmacAuthenticationHandler` as an authentication scheme. A request
that fails verification never reaches your endpoint.

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300);
        });
    });

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

Any policy that verifies is a success. To require a *specific* policy or
scheme per endpoint, see [authorization](../authorization/).

## AddHmacManager — manual control

Registers the components without touching authentication. You resolve a
manager and call it yourself.

```csharp
builder.Services.AddHmacManager(options =>
{
    options.AddPolicy("MyPolicy", policy =>
    {
        policy.UsePublicKey(publicKey);
        policy.UsePrivateKey(privateKey);
        policy.UseMemoryCache(maxAgeInSeconds: 300);
    });
});
```

`IHmacManagerFactory` is registered automatically, so it can be injected
anywhere:

```csharp
public class OrderClient(IHmacManagerFactory factory)
{
    public async Task SendAsync(HttpRequestMessage request)
    {
        var manager = factory.Create("MyPolicy")
            ?? throw new InvalidOperationException("no policy named MyPolicy");

        HmacResult result = await manager.SignAsync(request);
        // result.IsSuccess, result.Hmac
    }
}
```

`Create` returns `IHmacManager?` and gives back `null` when a name was given and
does not resolve — an unregistered policy, or a scheme that policy does not
declare. It does not throw, so an unhandled `null` is a null-reference at the
call site. A second argument selects a scheme:

```csharp
var manager = factory.Create("MyPolicy", "UserContext");
```

A blank scheme is not a failed lookup. `null`, `""` and whitespace all mean "no
scheme" — `IsNullOrWhiteSpace` decides it — so a value read from configuration
behaves the same however absence reaches you. A real name is not trimmed, so
`" UserContext "` is a different name and does not match.

```csharp
factory.Create("MyPolicy");                  // a manager, no scheme
factory.Create("MyPolicy", null);            // the same
factory.Create("MyPolicy", "");              // the same
factory.Create("MyPolicy", "UserContext");   // a manager using that scheme
factory.Create("MyPolicy", "Typo");          // null, and logs event 1202
```

The [TypeScript client](../../client/api/) answers each of these the same way.

Verifying by hand is the mirror image:

```csharp
HmacResult result = await manager.VerifyAsync(request);
if (!result.IsSuccess) { /* reject */ }
```

`HmacResult` carries `IsSuccess`, the `Hmac?` snapshot that was computed, and
`DateGenerated`.

## Adding schemes

Either entry point:

```csharp
options.AddPolicy("MyPolicy", policy =>
{
    policy.UsePublicKey(publicKey);
    policy.UsePrivateKey(privateKey);
    policy.AddScheme("UserContext", scheme =>
    {
        scheme.AddHeader("X-UserId", ClaimTypes.NameIdentifier);
        scheme.AddHeader("X-Email", ClaimTypes.Email);
    });
});
```

{{% hm-note %}}
Every header a scheme names must be on the request before `SignAsync` is
called. See [schemes](../../concepts/schemes/).
{{% /hm-note %}}

## Other ways in

- Bind policies from configuration instead of code — [configuration binding](../configuration-binding/)
- Sign every request from an `HttpClient` — [HttpClient](../http-client/)
- Load policies from a database — [dynamic policies](../dynamic-policies/)
