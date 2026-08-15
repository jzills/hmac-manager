---
title: Enforcement
description: Registering the provider, then choosing ingress gateway or ambient waypoint.
weight: 4
---

Installing the chart does not enforce anything. `istio.ingressGateway.enabled`
and `istio.waypoint.enabled` are both `false` by default, so a fresh install
runs the verifier without any traffic routed to it — you can deploy it, check
it is healthy, and turn it on separately.

Two steps: tell Istio the verifier exists, then point traffic at it.

## 1. Register the ext-authz provider

Istio needs an `extensionProviders` entry in its MeshConfig before any
`AuthorizationPolicy` can name it. `NOTES.txt` prints a ready-to-run script
after install; the manual equivalent:

```bash
kubectl patch configmap istio -n istio-system --type merge -p '{
  "data": {
    "mesh": "extensionProviders:\n- name: hmac-manager\n  envoyExtAuthzHttp:\n    service: hmac-manager.hmac-system.svc.cluster.local\n    port: 8080\n    includeRequestHeadersInCheck:\n      - authorization\n      - hmac-policy\n      - hmac-nonce\n      - hmac-daterequested\n    withRequestBody:\n      maxRequestBytes: 8192\n      allowPartialMessage: false\n"
  }
}'
kubectl rollout restart deployment/istiod -n istio-system
```

`includeRequestHeadersInCheck` is what forwards the HMAC headers to the
verifier — without them it cannot rebuild the
[signing content](../../concepts/signing-content/). `withRequestBody` is what
lets it hash the body; `maxRequestBytes` bounds how large a body it will read,
and `allowPartialMessage: false` means a body over that limit is rejected
rather than hashed incompletely, which would fail verification anyway.

{{% hm-note kind="warn" %}}
This patch replaces the `mesh` key. If your cluster already has MeshConfig
settings there, merge rather than overwrite — read the existing value first.
{{% /hm-note %}}

## 2. Point traffic at it

Each enabled point renders an `AuthorizationPolicy` with `action: CUSTOM`
targeting the named Gateway and calling the `hmac-manager` provider.

### North-south — external traffic

```bash
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.ingressGateway.enabled=true \
  --set istio.ingressGateway.name=<gateway-name> \
  --set istio.ingressGateway.namespace=<gateway-namespace>
```

Everything arriving through that ingress gateway must carry a valid signature.
This is the one you want for a public API.

### East-west — service to service

```bash
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.waypoint.enabled=true \
  --set istio.waypoint.name=<waypoint-name> \
  --set istio.waypoint.namespace=<waypoint-namespace>
```

Traffic between services through an ambient waypoint is checked. This one
requires Istio ambient mode and an existing waypoint.

Both can be enabled at once, and both can be set at install time instead of
upgrading.

## Which one

| | Ingress gateway | Ambient waypoint |
| --- | --- | --- |
| Covers | traffic entering the mesh | traffic between services inside it |
| Needs | an ingress Gateway | ambient mode and a waypoint |
| Typical use | a public or partner-facing API | internal calls you do not want spoofable |

Enforcing at the ingress does nothing for a caller already inside the mesh;
enforcing at a waypoint does nothing for external traffic. They answer
different questions, so enabling both is common.

## Checking

An unsigned request through an enforced point should return `403`. If
everything returns `403`, including correctly signed requests, the usual causes
are the provider not being registered, `includeRequestHeadersInCheck` missing a
header, or a policy in `Invalid` status — check
`kubectl get hmacpolicies -n hmac-system` first, then the verifier's logs at
`Debug`.
