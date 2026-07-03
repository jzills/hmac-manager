# zills/hmac-manager

Containerized HMAC authentication service for Kubernetes clusters running [Istio](https://istio.io/).

Runs as an [Envoy ext-authz](https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/filters/http/ext_authz/v3/ext_authz.proto) HTTP server. The Istio waypoint proxy or ingress gateway calls it before forwarding any inbound request — a valid HMAC signature passes, anything else is rejected with `403 Forbidden`.

```
Client → Istio waypoint / ingress gateway
              ↓ ext-authz check (original method + path)
         hmac-manager
              ↓ VerifyAsync
         200 OK → forward upstream
         403    → reject
```

## Deploy with Helm

The recommended way to run this image is via the [hmac-manager Helm chart](https://github.com/jzills/hmac-manager/tree/main/kubernetes/chart), which bundles Redis for replay protection and abstracts all configuration:

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

A fresh install deploys the verifier and a bundled Redis but does not enforce any traffic until you enable an Istio enforcement point (`istio.ingressGateway.*` or `istio.waypoint.*`). See the [chart documentation](https://artifacthub.io/packages/helm/zills/hmac-manager) for the full values reference.

## Tags

| Tag | Description |
|-----|-------------|
| `latest` | Most recent release |
| `X.Y.Z` | Specific release version |

## Environment variables

For advanced use cases or deployments without Helm.

| Variable | Required | Description |
|----------|----------|-------------|
| `ConnectionStrings__Redis` | No | Redis connection string. When set, enables the shared distributed nonce cache for multi-replica deployments. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` (default) or `Development`. `Development` activates the signing helper on port 8081. |
| `ASPNETCORE_URLS` | No | Listening URL (default: `http://+:8080`). |
| `SignPort` | No | Port for the dev-only signing helper (default: `8081`). |

Policies are loaded from a JSON config file mounted at `/etc/hmac-manager/config.json`:

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

Private keys are injected separately as environment variables (`HmacManager__0__Keys__PrivateKey`, etc.) and must never be written to the config file.

## Ports

| Port | Description |
|------|-------------|
| `8080` | ext-authz verification endpoint (all environments) |
| `8081` | dev-only signing helper (`Development` only, not exposed by the Kubernetes Service) |

## Replay protection

Each nonce is recorded until it expires. By default the nonce cache is in-process — safe for a single replica, not shared across pods. For multi-replica deployments, provide `ConnectionStrings__Redis` and set `Nonce.CacheType` to `Distributed` in the policy config. The Helm chart handles this automatically via `redis.enabled=true`.

## Signing requests (client side)

Use the [HmacManager NuGet package](https://www.nuget.org/packages/HmacManager) to sign outgoing requests from any .NET client.

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
