# zills/hmac-manager

Containerized HMAC authentication service for Kubernetes clusters running
[Istio](https://istio.io/).

Runs as an [Envoy ext-authz](https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/filters/http/ext_authz/v3/ext_authz.proto)
HTTP server. The Istio waypoint proxy or ingress gateway calls it before
forwarding any inbound request — a valid HMAC signature passes, anything else
is rejected with `403 Forbidden`.

```
Client → Istio waypoint / ingress gateway
              ↓ ext-authz check (original method + path)
         hmac-manager
              ↓ VerifyAsync
         200 OK → forward upstream
         403    → reject
```

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/kubernetes/)**

## Deploy with Helm

The recommended way to run this image is via the
[hmac-manager Helm chart](https://jzills.github.io/hmac-manager/docs/kubernetes/helm-chart/),
which bundles Redis for replay protection and abstracts all configuration:

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update

helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system \
  --create-namespace \
  --set "policies[0].name=my-policy" \
  --set "policies[0].publicKey=00000000-0000-0000-0000-000000000001" \
  --set "policies[0].privateKeySecret.name=my-hmac-secrets" \
  --set "policies[0].privateKeySecret.key=my-policy-privateKey"
```

A fresh install deploys the verifier and a bundled Redis but does not enforce
any traffic until you enable an Istio enforcement point
(`istio.ingressGateway.*` or `istio.waypoint.*`). See
[enforcement](https://jzills.github.io/hmac-manager/docs/kubernetes/enforcement/).

## Tags

| Tag | Description |
|-----|-------------|
| `latest` | Most recent release |
| `X.Y.Z` | Specific release version |

## Ports

| Port | Purpose |
|---|---|
| `8080` | ext-authz check endpoint — the one the Kubernetes Service exposes |
| `8081` | Signing helper, active only when `environment: Development`; never exposed by the Service |

## Environment variables

For advanced use cases or deployments without Helm.

| Variable | Required | Description |
|----------|----------|-------------|
| `ConnectionStrings__Redis` | No | Redis connection string. When set, enables the shared distributed nonce cache for multi-replica deployments. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` (default) or `Development`. `Development` activates the signing helper on port 8081. |
| `ASPNETCORE_URLS` | No | Listening URL (default: `http://+:8080`). |
| `SignPort` | No | Port for the dev-only signing helper (default: `8081`). |

Policies are loaded from a JSON config file mounted at
`/etc/hmac-manager/config.json`:

```json
{
  "HmacManager": [
    {
      "Name": "my-policy",
      "Keys": {
        "PublicKey": "00000000-0000-0000-0000-000000000001"
      },
      "Algorithms": {
        "ContentHashAlgorithm": "SHA256",
        "SigningHashAlgorithm": "HMACSHA256"
      },
      "Nonce": {
        "CacheType": "Distributed",
        "MaxAgeInSeconds": 60
      }
    }
  ]
}
```

Private keys are injected separately as environment variables
(`HmacManager__0__Keys__PrivateKey`, etc.) and must never be written to the
config file.

Under Helm, that file and those variables are maintained by the
[policy operator](https://hub.docker.com/r/zills/hmac-manager-operator) from
`HmacPolicy` custom resources — see
[the HmacPolicy CRD](https://jzills.github.io/hmac-manager/docs/kubernetes/hmacpolicy-crd/)
and the full
[chart values](https://jzills.github.io/hmac-manager/docs/reference/chart-values/).

## Replay protection

Every signature carries a nonce and a timestamp, both covered by the signature.
The verifier rejects a request outside the policy's window and rejects a nonce
it has already seen. Redis is bundled by the chart so this works across
replicas. See
[nonce and replay](https://jzills.github.io/hmac-manager/docs/concepts/nonce-and-replay/).

## Signing requests

Any of these produce signatures this service accepts:

- [HmacManager on NuGet](https://www.nuget.org/packages/HmacManager) — .NET clients
- [hmac-manager on npm](https://www.npmjs.com/package/hmac-manager) — browser and Node
- The `/sign` helper, in `Development` only — see
  [the ext-authz service](https://jzills.github.io/hmac-manager/docs/kubernetes/ext-authz-service/#development-signing-endpoint)

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
