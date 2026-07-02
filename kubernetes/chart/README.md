# hmac-manager

Istio ext-authz HTTP server for HMAC request authentication.

The waypoint proxy or ingress gateway calls this service before forwarding any inbound request. A valid HMAC signature passes; anything else is rejected with `403 Forbidden`. Redis is bundled automatically — no external dependencies to manage.

```
Client → Istio waypoint / ingress gateway
              ↓ ext-authz check
         hmac-manager (this chart)
              ↓ VerifyAsync
         200 OK → forward   |   403 → reject
```

## Prerequisites

- Kubernetes 1.25+
- Istio 1.21+ (ambient mode or gateway mode)
- Helm 3.10+
- An existing Kubernetes Secret containing each policy's private key (created externally — e.g. via [External Secrets Operator](https://external-secrets.io/))

## Install

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
```

```bash
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system \
  --create-namespace \
  --set "policies[0].name=MyPolicy" \
  --set "policies[0].publicKey=00000000-0000-0000-0000-000000000001" \
  --set "policies[0].privateKeySecret.name=my-hmac-secrets" \
  --set "policies[0].privateKeySecret.key=MyPolicy-privateKey"
```

Or with a values file:

```yaml
# values.yaml
policies:
  - name: MyPolicy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets   # pre-existing Secret, e.g. from External Secrets
      key: MyPolicy-privateKey
```

```bash
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system \
  --create-namespace \
  -f values.yaml
```

This deploys the hmac-manager verifier and a bundled Redis. By default it does **not** enforce anything yet — wiring it into the mesh is a deliberate second step (see [Enforce HMAC auth](#enforce-hmac-auth)).

## Register the ext-authz provider

After installation, NOTES.txt prints a ready-to-run script. The manual equivalent:

```bash
kubectl patch configmap istio -n istio-system --type merge -p '{
  "data": {
    "mesh": "extensionProviders:\n- name: hmac-manager\n  envoyExtAuthzHttp:\n    service: hmac-manager.hmac-system.svc.cluster.local\n    port: 8080\n    includeRequestHeadersInCheck:\n      - authorization\n      - hmac-policy\n      - hmac-nonce\n      - hmac-daterequested\n    withRequestBody:\n      maxRequestBytes: 8192\n      allowPartialMessage: false\n"
  }
}'
kubectl rollout restart deployment/istiod -n istio-system
```

## Enforce HMAC auth

Enforcement is opt-in: `istio.ingressGateway.enabled` and `istio.waypoint.enabled` are both `false` by default, so a fresh install runs the verifier without routing any traffic to it. Enable an enforcement point by pointing it at an existing Gateway resource:

```bash
# External traffic — ingress gateway
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.ingressGateway.enabled=true \
  --set istio.ingressGateway.name=<gateway-name> \
  --set istio.ingressGateway.namespace=<gateway-namespace>
```

```bash
# East-west traffic — ambient waypoint
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.waypoint.enabled=true \
  --set istio.waypoint.name=<waypoint-name> \
  --set istio.waypoint.namespace=<waypoint-namespace>
```

Each enabled point renders an `AuthorizationPolicy` (`action: CUSTOM`) that targets the named Gateway and calls the registered `hmac-manager` provider. You can also set these at install time instead of upgrading.

## Configuration

### Policies

At least one policy is required. Private keys must live in a pre-existing Kubernetes Secret — never pass them as chart values.

```yaml
policies:
  - name: MyPolicy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets     # name of a pre-existing Secret
      key: MyPolicy-privateKey  # key within that Secret
    algorithms:
      contentHash: SHA256       # SHA1 | SHA256 | SHA512   (default: SHA256)
      signingHash: HMACSHA256   # HMACSHA1 | HMACSHA256 | HMACSHA512 (default: HMACSHA256)
    nonce:
      maxAgeInSeconds: 60       # replay attack window in seconds (default: 60)
    schemes:                    # optional: named header sets included in the signature
      - name: UserScheme
        headers:
          - name: X-UserId
            claimType: userId
```

The policy `name` becomes the name of an `HmacPolicy` resource, so it must be a valid RFC 1123 subdomain (lowercase alphanumerics, `-` and `.`).

#### How policies work (operator + CRD)

Each entry in `policies` is rendered as an `HmacPolicy` custom resource (`hmac-manager.io/v1alpha1`). A controller — deployed by this chart — watches those resources and reconciles them into the ConfigMap and Secret that the verifier pods mount. The custom resource is the single source of truth, so you can also manage policies directly:

```bash
kubectl get hmacpolicies -n hmac-system
kubectl describe hmacpolicy payments-api -n hmac-system   # .status shows Ready / Invalid + why
```

Note: a policy defined in `values.policies` is Helm-managed — if you also edit that same policy via `kubectl`, `helm upgrade` will revert it. Keep a given policy name in one place: `values.policies` **or** `kubectl`, not both.

Policy changes — including updating `privateKeySecretRef` to point at a rotated key — take effect on running pods without a restart: the operator updates the ConfigMap and Secret, and each pod picks up the change the next time kubelet syncs its mounted volumes (typically within about a minute). Key rotation is an instant cutover: once a pod reloads, requests signed with the old key are rejected, so coordinate rotation with whoever holds the key.

### Redis (replay protection)

Redis is deployed as part of this release when `redis.enabled=true` (the default). All policies automatically use the distributed nonce cache. No connection strings, no external Redis cluster required.

| Value | Default | Description |
|---|---|---|
| `redis.enabled` | `true` | Deploy bundled Redis and use distributed nonce cache. Set to `false` for single-replica deployments only. |

When `redis.enabled=false` the chart refuses `replicaCount > 1` — the in-process nonce cache is not shared across pods.

### Other values

| Value | Default | Description |
|---|---|---|
| `environment` | `Production` | `Production` or `Development`. `Development` activates the signing helper endpoint on port 8081. |
| `replicaCount` | `1` | Number of replicas. Values > 1 require `redis.enabled=true`. |
| `namespace` | `hmac-system` | Namespace to deploy into. |
| `image.repository` | `zills/hmac-manager` | Container image repository. |
| `image.tag` | `0.1.0` | Container image tag. |
| `operator.image.repository` | `zills/hmac-manager-operator` | Operator (policy controller) image repository. |
| `operator.image.tag` | `0.1.0` | Operator image tag. |
| `service.port` | `8080` | Port the ext-authz service listens on. |
| `istio.enabled` | `true` | Master switch for Istio integration and the NOTES MeshConfig instructions. |
| `istio.ingressGateway.enabled` | `false` | Enforce inbound (ingress gateway) traffic. Requires `name` + `namespace`. |
| `istio.ingressGateway.name` | `""` | Name of the existing Gateway to target. Required when enabled. |
| `istio.ingressGateway.namespace` | `""` | Namespace of that Gateway. Required when enabled. |
| `istio.waypoint.enabled` | `false` | Enforce east-west (ambient mode) traffic. Requires `name` + `namespace`. |
| `istio.waypoint.name` | `""` | Name of the waypoint Gateway. Required when enabled. |
| `istio.waypoint.namespace` | `""` | Namespace of the waypoint. Required when enabled. |

## Multiple policies

Add additional entries to the `policies` list. Each policy can have its own keys, algorithms, nonce TTL, and schemes:

```yaml
policies:
  - name: PublicAPI
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: api-secrets
      key: public-api-private-key

  - name: InternalService
    publicKey: "00000000-0000-0000-0000-000000000002"
    privateKeySecret:
      name: api-secrets
      key: internal-service-private-key
    algorithms:
      signingHash: HMACSHA512
    nonce:
      maxAgeInSeconds: 30
```

## Signing requests (client side)

Use the [HmacManager NuGet package](https://www.nuget.org/packages/HmacManager) to sign outgoing requests from any .NET client.

## Development signing endpoint

Set `environment: Development` to activate a `/sign` helper endpoint on port 8081 (not exposed by the Kubernetes Service). Port-forward to reach it:

```bash
kubectl port-forward deploy/hmac-manager 9090:8081 -n hmac-system

curl -s -X POST http://localhost:9090/sign \
  -H "Content-Type: application/json" \
  -d '{"policy":"MyPolicy","method":"GET","uri":"http://echo.default.svc.cluster.local/"}'
```

Returns the HMAC headers to attach to your request. Never use `Development` in a production cluster.

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
