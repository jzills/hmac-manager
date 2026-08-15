---
title: Samples
description: Runnable ASP.NET Core solutions, each a web app calling a protected API.
weight: 7
---

Five solutions in [`samples/`](https://github.com/jzills/hmac-manager/tree/main/samples).
Each is the same shape — a web app signing requests to an API that verifies
them — differing in one thing, so the diff between two of them is the feature.

| Sample | Shows |
| --- | --- |
| [Simple authentication](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthentication) | The baseline: `AddHmac` on the API, `AddHmacHttpMessageHandler` on the client. Start here. |
| [Json configuration](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithJsonConfiguration) | Policies bound from an `IConfigurationSection` instead of code — see [configuration binding](../dotnet/configuration-binding/). |
| [Authorization policies](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithAuthorizationPolicies) | Requiring a specific policy and scheme per endpoint — see [authorization](../dotnet/authorization/). |
| [JavaScript client](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithJavaScriptClient) | Browser-side signing with the npm package against the same API. |
| [Scoped policies](https://github.com/jzills/hmac-manager/tree/main/samples/WebToApiAuthenticationWithScopedPolicies) | Policies resolved per request — see [dynamic policies](../dotnet/dynamic-policies/). |

## Running one

Each sample is a solution with a `Web` and an `Api` project. Both have to be
running: start the API first, then the web app, which calls it.

```bash
cd samples/WebToApiAuthentication
dotnet run --project Api
dotnet run --project Web    # in a second terminal
```

The keys in the samples are literals committed to the repository. They are
there so the sample runs with no setup — they are not examples of key handling.
See [configuration binding](../dotnet/configuration-binding/) for where a real
key belongs.

{{% hm-note %}}
The samples are the least maintained corner of the repository — a couple of the
per-project READMEs are stubs. The code runs; the prose around it may be thin.
For explanation, prefer these docs.
{{% /hm-note %}}
