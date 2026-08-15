---
title: Custom signing content
description: Replacing the default signing string, and what you take responsibility for.
weight: 7
---

A policy can build its own signing content instead of using the
[default format](../../concepts/signing-content/):

```csharp
policy.UseSigningContentBuilder(context =>
{
    var method = context.Request.Method;
    var suffix = $"{context.DateRequested}:{context.Nonce}";
    return $"{method}:{suffix}";
});
```

The delegate receives a `SigningContentContext` and returns the string to hash.

| Property | Type |
| --- | --- |
| `Request` | `HttpRequestMessage?` |
| `PublicKey` | `Guid?` |
| `DateRequested` | `DateTimeOffset?` |
| `Nonce` | `Guid?` |
| `HeaderValues` | `IReadOnlyCollection<HeaderValue>` |
| `ContentHash` | `string?` |

{{% hm-note kind="warn" %}}
Nothing is added for you. The default builder includes the nonce and the
timestamp because those are what make a signature single-use and short-lived —
omit them and you have built a signature that never expires and can be replayed
forever, while the nonce cache and the max-age check still appear to be
configured. Include `context.Nonce` and `context.DateRequested` unless you can
explain precisely why not.
{{% /hm-note %}}

## When this is the right tool

Interoperating with an existing HMAC scheme you do not control — a partner API
whose signing format is already fixed. That is essentially the whole list.

If you are choosing a format freely, the default is the one both other
implementations already speak.

## What you give up

The .NET default, the TypeScript client and the mesh verifier all build the
same string. A custom builder is only used by the side that configures it, so:

- the [TypeScript client](../../client/) needs a matching
  `signingContentAccessor` on its policy, written separately;
- the [mesh verifier](../../kubernetes/) has no equivalent hook, so a policy
  with custom content cannot be verified by the ext-authz service.

Both sides must produce byte-identical strings for the same request. That is
easy to get wrong across two languages — `DateTimeOffset.ToString()` and
`Date.toString()` do not agree, so interpolating `context.DateRequested`
directly, as the example above does, is only safe when both ends are .NET. Use
an explicit, unambiguous formatting such as
`context.DateRequested?.ToUnixTimeMilliseconds()` when they are not.

## Checking it

Turn `Trace` on for `HmacManager.Components.HmacManager` on both ends and
compare the content strings — event 1002 on the signer, event 1105 on the
verifier. See [logging](../logging/#diagnosing-a-signature-mismatch).
