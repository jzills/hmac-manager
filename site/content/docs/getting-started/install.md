---
title: Install
description: The three artifacts, where each one runs, and how they are versioned.
weight: 1
---

HmacManager ships as three independently versioned artifacts. They are not
alternatives to one another so much as three places the same check can happen.

| Artifact | Runs | Install |
| --- | --- | --- |
| [`HmacManager`](https://www.nuget.org/packages/HmacManager/) | In your ASP.NET Core process | `dotnet add package HmacManager` |
| [Helm chart](https://artifacthub.io/packages/helm/zills/hmac-manager) | In your cluster, as an Istio ext-authz service | `helm install hmac-manager zills/hmac-manager` |
| [`hmac-manager`](https://www.npmjs.com/package/hmac-manager) | In a browser or Node client, signing only | `npm install hmac-manager` |

## The .NET library

```bash
dotnet add package HmacManager
```

Targets `net8.0` and `net10.0`. It signs outgoing requests, verifies incoming
ones, or both. Continue with the [.NET quickstart](../dotnet-quickstart/).

## Kubernetes

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
```

The chart is also published to GHCR as an OCI artifact:

```bash
helm install hmac-manager oci://ghcr.io/jzills/charts/hmac-manager
```

Container images are on Docker Hub — [`zills/hmac-manager`](https://hub.docker.com/r/zills/hmac-manager)
for the verifier and [`zills/hmac-manager-operator`](https://hub.docker.com/r/zills/hmac-manager-operator)
for the policy controller. Continue with the
[Kubernetes quickstart](../kubernetes-quickstart/).

## The JavaScript client

```bash
npm install hmac-manager
```

Signs requests; it does not verify them. Ships ESM, CJS and type declarations.
Continue with the [client quickstart](../client-quickstart/).

{{% hm-note kind="warn" %}}
The .NET package and the npm package share a version line but not a version
number — they are released separately, so the numbers drift. Neither is a
lower bound on the other; both speak the same wire format.
{{% /hm-note %}}
