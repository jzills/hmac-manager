---
title: Kubernetes quickstart
description: Install the chart, register the ext-authz provider, and turn enforcement on.
weight: 3
---

Three steps, deliberately separate: install the verifier, tell Istio it exists,
then point traffic at it. A fresh install enforces nothing, so you can put it in
a cluster before you are ready to reject anything.

## Prerequisites

- Kubernetes 1.25+
- Istio 1.21+ (ambient or gateway mode)
- Helm 3.10+
- A Kubernetes Secret already holding each policy's private key. Create it
  outside the chart — for example with
  [External Secrets Operator](https://external-secrets.io/). Private keys are
  never chart values.

## 1. Install

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

At least one policy is required. This deploys the verifier, the policy
operator, and a bundled Redis for replay protection.

## 2. Register the ext-authz provider

Istio needs to know the verifier exists before it can call it. `NOTES.txt`
prints a ready-to-run script after install; the manual equivalent is:

```bash
kubectl patch configmap istio -n istio-system --type merge -p '{
  "data": {
    "mesh": "extensionProviders:\n- name: hmac-manager\n  envoyExtAuthzHttp:\n    service: hmac-manager.hmac-system.svc.cluster.local\n    port: 8080\n    includeRequestHeadersInCheck:\n      - authorization\n      - hmac-policy\n      - hmac-nonce\n      - hmac-daterequested\n    withRequestBody:\n      maxRequestBytes: 8192\n      allowPartialMessage: false\n"
  }
}'
kubectl rollout restart deployment/istiod -n istio-system
```

## 3. Turn enforcement on

Both enforcement points default to `false`, so nothing is rejected until you
enable one. Point it at an existing Gateway:

```bash
# North-south — external traffic through an ingress gateway
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.ingressGateway.enabled=true \
  --set istio.ingressGateway.name=<gateway-name> \
  --set istio.ingressGateway.namespace=<gateway-namespace>
```

```bash
# East-west — service-to-service through an ambient waypoint
helm upgrade hmac-manager zills/hmac-manager \
  --namespace hmac-system --reuse-values \
  --set istio.waypoint.enabled=true \
  --set istio.waypoint.name=<waypoint-name> \
  --set istio.waypoint.namespace=<waypoint-namespace>
```

Each enabled point renders an `AuthorizationPolicy` with `action: CUSTOM`
targeting that Gateway. See [enforcement](../../kubernetes/enforcement/) for
which one you want.

## Check it worked

```bash
kubectl get hmacpolicies -n hmac-system
kubectl describe hmacpolicy my-policy -n hmac-system   # .status shows Ready or Invalid, and why
```

An unsigned request through an enforced gateway should now come back `403`.

{{% hm-note %}}
In a `Development` install the chart exposes a `/sign` helper on port 8081 so
you can produce a valid signature without writing a client. See the
[ext-authz service](../../kubernetes/ext-authz-service/#development-signing-endpoint).
{{% /hm-note %}}

## Next

- [The HmacPolicy CRD](../../kubernetes/hmacpolicy-crd/) — managing policies as resources instead of chart values
- [Chart values](../../reference/chart-values/) — the full reference
- [Enforcement](../../kubernetes/enforcement/) — ingress versus waypoint
