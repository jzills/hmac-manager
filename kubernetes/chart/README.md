# hmac-manager

Istio ext-authz HTTP server for HMAC request authentication.

The waypoint proxy or ingress gateway calls this service before forwarding any
inbound request. A valid HMAC signature passes; anything else is rejected with
`403 Forbidden`. Redis is bundled automatically — no external dependencies to
manage.

```
Client → Istio waypoint / ingress gateway
              ↓ ext-authz check
         hmac-manager (this chart)
              ↓ VerifyAsync
         200 OK → forward   |   403 → reject
```

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/kubernetes/)**

## Prerequisites

- Kubernetes 1.25+
- Istio 1.21+ (ambient mode or gateway mode)
- Helm 3.10+
- An existing Kubernetes Secret containing each policy's private key (created
  externally — e.g. via [External Secrets Operator](https://external-secrets.io/))

## Install

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
```

```bash
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system \
  --create-namespace \
  --set "policies[0].name=my-policy" \
  --set "policies[0].publicKey=00000000-0000-0000-0000-000000000001" \
  --set "policies[0].privateKeySecret.name=my-hmac-secrets" \
  --set "policies[0].privateKeySecret.key=my-policy-privateKey"
```

At least one policy is required. This deploys the verifier, the policy operator
and a bundled Redis, but does **not** enforce anything yet — wiring it into the
mesh is a deliberate second step.

Full walkthrough, including registering the ext-authz provider in Istio's
MeshConfig and turning enforcement on:
**[Kubernetes quickstart](https://jzills.github.io/hmac-manager/docs/getting-started/kubernetes-quickstart/)**.

## Policies

Private keys must live in a pre-existing Kubernetes Secret — never pass them as
chart values.

```yaml
policies:
  - name: my-policy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets      # name of a pre-existing Secret
      key: my-policy-privateKey  # key within that Secret
    algorithms:
      contentHash: SHA256        # SHA1 | SHA256 | SHA512   (default: SHA256)
      signingHash: HMACSHA256    # HMACSHA1 | HMACSHA256 | HMACSHA512 (default: HMACSHA256)
    nonce:
      maxAgeInSeconds: 60        # replay attack window in seconds (default: 60)
    schemes:                     # optional: named header sets included in the signature
      - name: UserScheme
        headers:
          - name: X-UserId
            claimType: userId
```

Each entry is rendered as an `HmacPolicy` custom resource
(`hmac-manager.io/v1alpha1`), which a controller deployed by this chart
reconciles into the ConfigMap and Secret the verifier pods mount. Policy and
key changes take effect without a pod restart.

The policy `name` becomes a resource name, so it must be a valid RFC 1123
subdomain (lowercase alphanumerics, `-` and `.`).

> A policy defined in `values.policies` is Helm-managed. If you also edit it
> with `kubectl`, `helm upgrade` will revert it — keep a given policy name in
> one place, not both.

See [the HmacPolicy CRD](https://jzills.github.io/hmac-manager/docs/kubernetes/hmacpolicy-crd/).

## Values

### Redis (replay protection)

| Value | Default | Description |
|---|---|---|
| `redis.enabled` | `true` | Deploy bundled Redis and use the distributed nonce cache. Set to `false` for single-replica deployments only. |

When `redis.enabled=false` the chart refuses `replicaCount > 1` — the
in-process nonce cache is not shared across pods, so replay protection would
appear configured and do nothing.

### Everything else

| Value | Default | Description |
|---|---|---|
| `environment` | `Production` | `Production` or `Development`. `Development` activates the signing helper endpoint on port 8081. |
| `replicaCount` | `1` | Number of replicas. Values > 1 require `redis.enabled=true`. |
| `namespace` | `hmac-system` | Namespace to deploy into. |
| `image.repository` | `zills/hmac-manager` | Container image repository. |
| `image.tag` | pinned per release | Container image tag. |
| `operator.image.repository` | `zills/hmac-manager-operator` | Operator (policy controller) image repository. |
| `operator.image.tag` | pinned per release | Operator image tag. |
| `service.port` | `8080` | Port the ext-authz service listens on. |
| `logging.level` | `Information` | Verbosity of HmacManager's own log messages (signing, verification, policy reload, reconciliation) on both the server and the operator. `Trace` and `Debug` are per-request; `Information` covers policy/config changes only. Framework and dependency logging (ASP.NET Core, KubeOps, etc.) is suppressed below `Error` regardless of this setting. |
| `istio.enabled` | `true` | Master switch for Istio integration and the NOTES MeshConfig instructions. |
| `istio.ingressGateway.enabled` | `false` | Enforce inbound (ingress gateway) traffic. Requires `name` + `namespace`. |
| `istio.ingressGateway.name` | `""` | Name of the existing Gateway to target. Required when enabled. |
| `istio.ingressGateway.namespace` | `""` | Namespace of that Gateway. Required when enabled. |
| `istio.waypoint.enabled` | `false` | Enforce east-west (ambient mode) traffic. Requires `name` + `namespace`. |
| `istio.waypoint.name` | `""` | Name of the waypoint Gateway. Required when enabled. |
| `istio.waypoint.namespace` | `""` | Namespace of the waypoint. Required when enabled. |

## More

| | |
|---|---|
| [Enforcement](https://jzills.github.io/hmac-manager/docs/kubernetes/enforcement/) | Registering the provider; ingress gateway vs ambient waypoint |
| [The HmacPolicy CRD](https://jzills.github.io/hmac-manager/docs/kubernetes/hmacpolicy-crd/) | Managing policies with `kubectl`, status, key rotation |
| [The ext-authz service](https://jzills.github.io/hmac-manager/docs/kubernetes/ext-authz-service/) | Ports, image tags, the development signing endpoint |
| [Redis](https://jzills.github.io/hmac-manager/docs/kubernetes/redis/) | Replay protection and the replica constraint |
| [Chart values](https://jzills.github.io/hmac-manager/docs/reference/chart-values/) | The same table, kept with the rest of the docs |

Sign requests from any .NET client with the
[HmacManager NuGet package](https://www.nuget.org/packages/HmacManager), or
from a browser or Node with
[hmac-manager on npm](https://www.npmjs.com/package/hmac-manager).

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
