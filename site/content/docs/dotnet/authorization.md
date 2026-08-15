---
title: Authorization
description: Requiring a specific policy or scheme on an endpoint.
weight: 2
---

By default `AddHmac` authenticates any request that verifies against any
registered policy. That is often not enough: if you hold one policy for a
partner and another for an internal service, "verified" does not mean
"allowed here".

Four ways to narrow it, from most direct to most composable.

## The attribute

```csharp
[HmacAuthenticate(Policy = "HmacPolicy", Scheme = "HmacScheme")]
public class HomeController : Controller
{
}
```

## As an authorization requirement

`HmacAuthenticateAttribute` is also an `IAuthorizationRequirement`, so it can
go straight into a policy. `HmacAuthorizationHandler` is registered to handle
it.

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireHmac", policy =>
        policy.AddRequirements(new HmacAuthenticateAttribute
        {
            Policy = "HmacPolicy",
            Scheme = "HmacScheme"
        }));
});
```

## The builder extensions

`RequireHmacPolicy` and `RequireHmacScheme` add the same requirements more
readably, and compose with everything else on `AuthorizationPolicyBuilder`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireHmac", policy =>
    {
        policy.RequireHmacPolicy("HmacPolicy");
        policy.RequireHmacScheme("HmacScheme");
    });
});
```

Or both at once:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireHmac", policy =>
    {
        policy.RequireHmacAuthentication("HmacPolicy", "HmacScheme");
    });
});
```

Then use it as you would any authorization policy:

```csharp
[Authorize("RequireHmac")]
public class PaymentsController : ControllerBase
{
}
```

## Claims

When a request authenticates with a scheme, every header that scheme names is
added to the `ClaimsPrincipal`. The claim type is whatever the scheme declared,
or the header's own name if it declared none — so
`scheme.AddHeader("X-UserId", ClaimTypes.NameIdentifier)` makes
`User.FindFirst(ClaimTypes.NameIdentifier)` return the signed value.

Those values are covered by the signature, which is what makes them safe to
authorize on. An unsigned header is just a header. See
[schemes](../../concepts/schemes/).
