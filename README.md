
<div align="center">

<img src="assets/logo.svg" alt="HMAC MANAGER" width="470">

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![NuGet Downloads](https://img.shields.io/nuget/dt/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![npm Version](https://img.shields.io/npm/v/hmac-manager?logo=npm&label=npm)](https://www.npmjs.com/package/hmac-manager) [![Docker service image](https://img.shields.io/docker/v/zills/hmac-manager?logo=docker&label=service)](https://hub.docker.com/r/zills/hmac-manager) [![Docker operator image](https://img.shields.io/docker/v/zills/hmac-manager-operator?logo=docker&label=operator)](https://hub.docker.com/r/zills/hmac-manager-operator) [![Artifact Hub](https://img.shields.io/endpoint?url=https://artifacthub.io/badge/repository/zills)](https://artifacthub.io/packages/search?repo=zills) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

_Secure HMAC request authentication for ASP.NET Core — as a NuGet library, or as an Istio ext-authz service with a Kubernetes operator for declarative policies._

</div>

- [Summary](#summary)
- [Features](#features)
- [.NET Library](#net-library)
- [Kubernetes (Istio ext-authz)](#kubernetes-istio-ext-authz)
- [JavaScript / TypeScript Client](#javascript--typescript-client)
- [Resources](#resources)

## Summary

Add secure HMAC request authentication to ASP.NET Core APIs with lightweight, configurable middleware — or enforce it across a Kubernetes service mesh without touching application code.

## Features

**ASP.NET Core library**

- Policy-based signing and verification — validate requests against multiple named policies.
- Schemes — require specific header values per policy, with automatic mapping of those headers to claims.
- Built-in nonce management for replay protection, in-memory or Redis-backed.
- Dynamic policies — a singleton collection, or pulled at runtime from a database or other store.
- First-class ASP.NET Core authentication/authorization integration, plus client-side request signing via an `HttpClient` handler.
- Targets `net8.0` and `net10.0`.

**Kubernetes / Istio**

- Envoy/Istio **ext-authz** service — enforce HMAC at the ingress edge or between services (ambient waypoints); requests without a valid signature are rejected with `403`, no application changes required.
- **`HmacPolicy` CRD + operator** — declare policies as Kubernetes resources; the operator reconciles them into the ConfigMap and Secret the verifier mounts.
- Private keys sourced from Kubernetes **Secrets**; policy and key changes **hot-reload without a pod restart**.
- Redis bundled for replay protection — no external dependencies to provision.
- Installable via Helm and indexed on Artifact Hub.

## .NET Library

`HmacManager` is available on [NuGet](https://www.nuget.org/packages/HmacManager/).

```bash
dotnet add package HmacManager
```

Register a policy and let the built-in authentication handler verify incoming requests:

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300); // nonce / replay window
        });
    });
```

On the calling side, sign outgoing requests automatically by attaching the handler to an `HttpClient`:

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy");
```

See the [library documentation](src/README.md) for schemes, dynamic policies, `HmacEvents`, `IConfiguration` binding, and custom signing content.

## Kubernetes (Istio ext-authz)

HmacManager also ships a containerized verification service and a Helm chart for enforcing HMAC authentication in a Kubernetes mesh — at the ingress edge or between services — without changing application code. The service runs as an [Envoy ext-authz](https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/filters/http/ext_authz/v3/ext_authz.proto) HTTP server: an Istio ingress gateway (north-south) or an ambient waypoint (east-west, service-to-service) calls it before forwarding a request, and anything without a valid HMAC signature is rejected with `403`. Redis is bundled for replay protection — no external dependencies to provision.

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system --create-namespace \
  --set "policies[0].name=my-policy" \
  --set "policies[0].publicKey=00000000-0000-0000-0000-000000000001" \
  --set "policies[0].privateKeySecret.name=my-hmac-secrets" \
  --set "policies[0].privateKeySecret.key=my-policy-privateKey"
```

Prefer GitOps? Declare policies as `HmacPolicy` custom resources and let the operator reconcile them into the config the verifier mounts — no `--set` flags required:

```yaml
apiVersion: hmac-manager.io/v1alpha1
kind: HmacPolicy
metadata:
  name: my-policy
  namespace: hmac-system
spec:
  publicKey: "00000000-0000-0000-0000-000000000001"
  privateKeySecretRef:
    name: my-hmac-secrets
    key: my-policy-privateKey
  algorithms:
    contentHash: SHA256      # SHA1 | SHA256 | SHA512
    signingHash: HMACSHA256  # HMACSHA1 | HMACSHA256 | HMACSHA512
  nonce:
    maxAgeInSeconds: 300
```

```bash
kubectl apply -f my-policy.yaml
```

- **Helm chart** — [kubernetes/chart](kubernetes/chart/README.md) · [Artifact Hub](https://artifacthub.io/packages/search?repo=zills)
- **Container images** — [zills/hmac-manager](https://hub.docker.com/r/zills/hmac-manager) (ext-authz service) · [zills/hmac-manager-operator](https://hub.docker.com/r/zills/hmac-manager-operator) (policy operator) on Docker Hub

## JavaScript / TypeScript Client

`hmac-manager` is available on [npm](https://www.npmjs.com/package/hmac-manager).

```bash
npm install hmac-manager
```

Sign requests from a browser or Node client so they verify against an `HmacManager`-protected API.

```ts
import { HmacManagerFactory, HashAlgorithm } from "hmac-manager";

const factory = new HmacManagerFactory([{
  name: "MyPolicy",
  publicKey: "00000000-0000-0000-0000-000000000001",
  privateKey: "zvg29s2cQ4idOqbUJWETOw==",
  contentHashAlgorithm: HashAlgorithm.SHA256,
  signatureHashAlgorithm: HashAlgorithm.SHA256,
  schemes: []
}]);

const request = new Request("https://api.example.com/orders");
await factory.create("MyPolicy")!.sign(request); // adds the Hmac headers to `request`
const response = await fetch(request);
```

See the [client documentation](client/) for schemes and configuration.

## Resources

- [Library documentation](src/README.md)
- [Client documentation](client/)
- [Samples](samples/README.md)
