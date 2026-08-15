---
title: Samples
description: Runnable samples, each a client calling a protected API.
weight: 7
---

Six samples in [`samples/`](https://github.com/jzills/hmac-manager/tree/main/samples).
Each is the same shape — a client signing requests to an API that verifies
them — differing in **one** thing, so the diff between two of them is the
feature.

| Sample | Changes | Api | Client |
| --- | --- | --- | --- |
| [Simple authentication](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthentication) | The baseline: `AddHmac` on the API, `AddHmacHttpMessageHandler` on the client. Start here. | 5100 | 5101 |
| [Json configuration](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithJsonConfiguration) | Policies bound from an `IConfigurationSection` — see [configuration binding](../dotnet/configuration-binding/). | 5110 | 5111 |
| [Authorization policies](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithAuthorizationPolicies) | `[Authorize]` with `RequireHmacAuthentication` — see [authorization](../dotnet/authorization/). | 5120 | 5121 |
| [Scoped policies](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithScopedPolicies) | Policies resolved per request — see [dynamic policies](../dotnet/dynamic-policies/). | 5130 | 5131 |
| [JavaScript client](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithJavaScriptClient) | The client signs with the npm package instead of .NET — see [signing requests](../client/signing-requests/). | 5140 | — |
| [Node API](https://github.com/jzills/hmac-manager/tree/main/samples/NodeApiAuthentication) | The **API** verifies in Node, called by a Node client and a .NET client — see [verifying requests](../client/verifying-requests/). | 5200 | — |

## Running one

Each .NET sample has an `Api` and a client project. Both have to be running:
start the API first, then the client.

```bash
cd samples/WebToApiAuthentication
dotnet run --project Api
dotnet run --project Web    # in a second terminal
```

Then open the client's URL. It reports what the API returned for a signed `GET`
and a signed `POST`.

The two samples that use the TypeScript client link it from source, so build it
once first:

```bash
cd client/lib && npm ci && npm run build
```

Inside a sample use `npm install`, not `npm ci` — the dependency is a `file:`
link to `client/lib`, so there is nothing external to pin and no lockfile is
committed.

Everything listens on plain HTTP on localhost, so no development certificate is
needed. HMAC authenticates a request and detects tampering; it does not encrypt
one, so a real deployment still belongs behind TLS.

## The keys

The keys in the samples are literals committed to the repository. Every sample
uses the **same** key pair on purpose, so that the only difference between any
two of them is the feature being shown. They are not examples of key handling —
see [configuration binding](../dotnet/configuration-binding/) for where a real
key belongs.

{{% hm-note %}}
The samples are built on every pull request, and the two Node ones are run end
to end, by the `Sample Builds` job in `pr.yml`. A sample that stops compiling,
stops installing or stops round-tripping fails the build.
{{% /hm-note %}}
