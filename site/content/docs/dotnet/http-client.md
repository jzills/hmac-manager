---
title: Signing with HttpClient
linkTitle: HttpClient
description: Sign every outgoing request from a named client automatically.
weight: 4
---

`AddHmacHttpMessageHandler` attaches an `HmacDelegatingHandler` to a named
`HttpClient`. Every request that client sends is signed on the way out.

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy");
```

With a scheme:

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy", "UserContext");
```

Then use the client normally — there is nothing to remember at the call site:

```csharp
var client = httpClientFactory.CreateClient("api");
var response = await client.GetAsync("/orders");
```

## Why this rather than signing by hand

The handler runs inside the `HttpClient` pipeline, which means it signs *after*
`BaseAddress` has been applied and the request URI is absolute.

Signing manually before calling `SendAsync` is the common way to get a
signature mismatch on every request: you sign `/orders`, the server verifies
against `https://api.example.com/orders`, and the authority segment of the
[signing content](../../concepts/signing-content/) does not match. Signing a
relative URI throws `AbsoluteUriException` rather than producing that quietly.

## Failures

If a request cannot be signed, the handler throws `HmacSigningException` rather
than sending an unsigned request. The reason is logged first — event 1001 for a
signing that produced no HMAC, event 1003 for the abandoned request.

The most common cause is a scheme header that is not on the request yet:

```csharp
var request = new HttpRequestMessage(HttpMethod.Get, "/orders");
request.Headers.Add("X-UserId", "42");     // before Send, not after
var response = await client.SendAsync(request);
```

{{% hm-note %}}
Scheme headers must be present before `Send` or `SendAsync`. The handler signs
what it is given; a header added later is not covered by the signature and the
verifier will reject the request.
{{% /hm-note %}}
