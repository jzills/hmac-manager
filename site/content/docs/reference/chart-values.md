---
title: Chart values
description: Every value the Helm chart accepts.
weight: 3
---

## Policies

At least one is required.

```yaml
policies:
  - name: my-policy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets
      key: my-policy-privateKey
    algorithms:
      contentHash: SHA256
      signingHash: HMACSHA256
    nonce:
      maxAgeInSeconds: 60
    schemes:
      - name: UserScheme
        headers:
          - name: X-UserId
            claimType: userId
```

| Field | Default | Notes |
| --- | --- | --- |
| `name` | — | Required. Becomes an `HmacPolicy` resource name, so it must be a valid RFC 1123 subdomain. |
| `publicKey` | — | Required. |
| `privateKeySecret.name` | — | Name of a **pre-existing** Secret. |
| `privateKeySecret.key` | — | Key within that Secret. |
| `algorithms.contentHash` | `SHA256` | `SHA1`, `SHA256`, `SHA512` |
| `algorithms.signingHash` | `HMACSHA256` | `HMACSHA1`, `HMACSHA256`, `HMACSHA512` |
| `nonce.maxAgeInSeconds` | `60` | Replay window, in seconds. |
| `schemes[].name` | — | Scheme name. |
| `schemes[].headers[].name` | — | Header name. |
| `schemes[].headers[].claimType` | header name | Claim the header maps to. |

Private keys are never chart values — only a reference to a Secret that
already exists.

## Redis

| Value | Default | Description |
| --- | --- | --- |
| `redis.enabled` | `true` | Deploy bundled Redis and use the distributed nonce cache. Set `false` only for single-replica deployments; the chart then refuses `replicaCount > 1`. |

## Everything else

| Value | Default | Description |
| --- | --- | --- |
| `environment` | `Production` | `Production` or `Development`. `Development` activates the signing helper on port 8081. |
| `replicaCount` | `1` | Number of replicas. Values above 1 require `redis.enabled=true`. |
| `namespace` | `hmac-system` | Namespace to deploy into. |
| `image.repository` | `zills/hmac-manager` | Verifier image repository. |
| `image.tag` | pinned per chart release | Verifier image tag. See `values.yaml` for the version a given chart pins. |
| `image.pullPolicy` | `IfNotPresent` | Verifier image pull policy. |
| `operator.image.repository` | `zills/hmac-manager-operator` | Operator image repository. |
| `operator.image.tag` | pinned per chart release | Operator image tag. Versioned separately from the verifier. |
| `service.port` | `8080` | Port the ext-authz service listens on. |
| `logging.level` | `Information` | Verbosity of HmacManager's own messages on both the verifier and the operator. `Trace` and `Debug` are per-request; `Information` covers policy and config changes only. Framework logging (ASP.NET Core, KubeOps) is suppressed below `Error` regardless. |
| `istio.enabled` | `true` | Master switch for Istio integration and the MeshConfig instructions in NOTES.txt. |
| `istio.ingressGateway.enabled` | `false` | Enforce on ingress gateway traffic. Requires `name` and `namespace`. |
| `istio.ingressGateway.name` | `""` | Name of the existing Gateway to target. |
| `istio.ingressGateway.namespace` | `""` | Namespace of that Gateway. |
| `istio.waypoint.enabled` | `false` | Enforce on ambient waypoint traffic. Requires `name` and `namespace`. |
| `istio.waypoint.name` | `""` | Name of the waypoint Gateway. |
| `istio.waypoint.namespace` | `""` | Namespace of the waypoint. |

The chart ships a `values.schema.json`, so `helm install` rejects a malformed
values file before anything reaches the cluster.

See [the Helm chart](../../kubernetes/helm-chart/) for how to use these and
[enforcement](../../kubernetes/enforcement/) for the Istio side.
