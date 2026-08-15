---
title: .NET quickstart
description: Verify incoming requests in an API and sign outgoing ones from a client.
weight: 2
---

Two processes: an API that verifies, and a client that signs. Both need the
same policy name and the same key pair.

## Verify incoming requests

`AddHmac` registers the authentication handler, so verification happens in the
authentication pipeline and a request that fails never reaches your endpoint.

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

Then protect endpoints as you would with any other scheme:

```csharp
[Authorize(AuthenticationSchemes = HmacAuthenticationDefaults.AuthenticationScheme)]
public class OrdersController : ControllerBase { }
```

The public key is not a secret — it travels in the signing content and
identifies which key pair to verify against. The private key is the secret, and
it never leaves either process.

## Sign outgoing requests

On the calling side, attach the handler to a named `HttpClient`. Every request
that client sends is signed.

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy");
```

```csharp
var client = httpClientFactory.CreateClient("api");
var response = await client.GetAsync("/orders");
```

## Check it worked

A signed request carries these headers:

```http
GET /orders HTTP/1.1
Host: api.example.com
Authorization: Hmac <signature>
Hmac-Policy: MyPolicy
Hmac-Nonce: 6f9619ff-8b86-d011-b42d-00c04fc964ff
Hmac-DateRequested: 1723651200000
```

If verification fails, turn the `HmacManager` log category up to `Debug` and
the rejection will say which check failed rather than only that one did — see
[logging](../../dotnet/logging/).

{{% hm-note %}}
`UseMemoryCache` keeps used nonces in process. That is correct for a single
instance; behind a load balancer, two instances would not share the cache and a
replayed request could land on the one that has not seen it. Use
[`UseDistributedCache`](../../concepts/nonce-and-replay/) there.
{{% /hm-note %}}

## Next

- [Registration](../../dotnet/registration/) — the other ways to register, and when to use them
- [Schemes](../../concepts/schemes/) — folding header values into the signature
- [Policies](../../concepts/policies/) — what a policy actually contains
