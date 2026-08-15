---
title: The Helm chart
description: Installing, declaring policies, and running several of them.
weight: 2
---

One chart deploys the verifier, the policy operator and Redis.

## Prerequisites

- Kubernetes 1.25+
- Istio 1.21+ (ambient or gateway mode)
- Helm 3.10+
- A Kubernetes Secret already holding each policy's private key, created
  outside the chart — for example with
  [External Secrets Operator](https://external-secrets.io/)

## Install

```bash
helm repo add zills https://jzills.github.io/hmac-manager
helm repo update
```

The chart is also an OCI artifact on GHCR:

```bash
helm install hmac-manager oci://ghcr.io/jzills/charts/hmac-manager
```

At least one policy is required:

```bash
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system \
  --create-namespace \
  --set "policies[0].name=my-policy" \
  --set "policies[0].publicKey=00000000-0000-0000-0000-000000000001" \
  --set "policies[0].privateKeySecret.name=my-hmac-secrets" \
  --set "policies[0].privateKeySecret.key=my-policy-privateKey"
```

Or with a values file, which is easier to live with:

```yaml
# values.yaml
policies:
  - name: my-policy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets      # pre-existing Secret
      key: my-policy-privateKey
```

```bash
helm install hmac-manager zills/hmac-manager \
  --namespace hmac-system --create-namespace -f values.yaml
```

This deploys the verifier and Redis but **enforces nothing** — wiring it into
the mesh is a deliberate second step. See [enforcement](../enforcement/).

## Declaring a policy

```yaml
policies:
  - name: my-policy
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: my-hmac-secrets      # name of a pre-existing Secret
      key: my-policy-privateKey  # key within that Secret
    algorithms:
      contentHash: SHA256        # SHA1 | SHA256 | SHA512        (default SHA256)
      signingHash: HMACSHA256    # HMACSHA1 | HMACSHA256 | HMACSHA512 (default HMACSHA256)
    nonce:
      maxAgeInSeconds: 60        # replay window (default 60)
    schemes:                     # optional
      - name: UserScheme
        headers:
          - name: X-UserId
            claimType: userId
```

{{% hm-note kind="warn" %}}
Private keys are never chart values. `privateKeySecret` references a Secret
that already exists; the chart does not create it, and a key passed as a value
would end up in the release's stored manifest.
{{% /hm-note %}}

The policy `name` becomes the name of an `HmacPolicy` resource, so it must be a
valid RFC 1123 subdomain — lowercase alphanumerics, `-` and `.`.

## Several policies

Each entry gets its own keys, algorithms, replay window and schemes:

```yaml
policies:
  - name: public-api
    publicKey: "00000000-0000-0000-0000-000000000001"
    privateKeySecret:
      name: api-secrets
      key: public-api-private-key

  - name: internal-service
    publicKey: "00000000-0000-0000-0000-000000000002"
    privateKeySecret:
      name: api-secrets
      key: internal-service-private-key
    algorithms:
      signingHash: HMACSHA512
    nonce:
      maxAgeInSeconds: 30
```

## Values

The full table is in the [chart values reference](../../reference/chart-values/).

## Next

- [The HmacPolicy CRD](../hmacpolicy-crd/) — managing policies with `kubectl` instead
- [Enforcement](../enforcement/) — ingress gateway versus ambient waypoint
- [Redis](../redis/) — replay protection and the replica constraint
