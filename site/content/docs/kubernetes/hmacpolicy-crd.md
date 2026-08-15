---
title: The HmacPolicy CRD
description: Declaring policies as Kubernetes resources, and how the operator reconciles them.
weight: 3
---

Every policy is an `HmacPolicy` custom resource
(`hmac-manager.io/v1alpha1`). The chart renders one per entry in
`values.policies`, and you can also create them directly — the resource is the
source of truth either way.

An operator deployed by the chart watches them and reconciles them into the
aggregate ConfigMap and Secret that the verifier pods mount.

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
  schemes:
    - name: UserScheme
      headers:
        - name: X-UserId
          claimType: userId
```

```bash
kubectl apply -f my-policy.yaml
```

## The spec

| Field | Required | Notes |
| --- | --- | --- |
| `publicKey` | yes | The policy's public key |
| `privateKeySecretRef.name` | with the ref | Name of an existing Secret |
| `privateKeySecretRef.key` | with the ref | Key within that Secret |
| `algorithms.contentHash` | no | `SHA1`, `SHA256`, `SHA512` |
| `algorithms.signingHash` | no | `HMACSHA1`, `HMACSHA256`, `HMACSHA512` |
| `nonce.maxAgeInSeconds` | no | Integer, minimum 1 |
| `schemes[].name` | with a scheme | Scheme name |
| `schemes[].headers[].name` | with a header | Header name |
| `schemes[].headers[].claimType` | no | Defaults to the header name |

The resource is namespaced, with the short name `hmacpol`.

## Status

The operator writes back a `status` saying whether the policy was accepted, so
a bad policy reports itself rather than failing silently at request time.

```bash
kubectl get hmacpolicies -n hmac-system
```

```
NAME        PUBLIC KEY                             READY   AGE
my-policy   00000000-0000-0000-0000-000000000001   Ready   4m
```

```bash
kubectl describe hmacpolicy my-policy -n hmac-system
```

`.status.phase` is `Ready` or `Invalid`, and `.status.message` says why when it
is `Invalid` — a missing Secret, an unparseable key, an algorithm that is not
one of the permitted values.

## Changes take effect without a restart

The operator updates the ConfigMap and Secret; each verifier pod picks the
change up the next time kubelet syncs its mounted volumes, typically within
about a minute. That covers rotating a key by pointing `privateKeySecretRef`
at a new one.

{{% hm-note kind="warn" %}}
Key rotation is an instant cutover, not an overlap. Once a pod reloads,
requests signed with the old key are rejected — there is no window where both
are accepted. Coordinate with whoever holds the key.
{{% /hm-note %}}

## Helm-managed or kubectl-managed, not both

A policy defined in `values.policies` is owned by Helm. Editing that same
policy with `kubectl` works until the next `helm upgrade`, which reverts it.

Keep a given policy name in one place — `values.policies` **or** `kubectl`.
Mixing them produces changes that disappear later for no visible reason.

## The operator

[`zills/hmac-manager-operator`](https://hub.docker.com/r/zills/hmac-manager-operator),
a KubeOps controller. The chart wires its namespaced RBAC and configuration; it
is not meant to run standalone.

| Env var | Purpose |
| --- | --- |
| `Operator__WatchNamespace` | Namespace to watch, and to write the aggregate ConfigMap/Secret into |
| `Operator__ConfigMapName` | Name of the aggregate ConfigMap the verifier mounts |
| `Operator__SecretName` | Name of the aggregate Secret the verifier mounts |
| `Operator__NonceCacheType` | `Memory` or `Distributed`, applied to every rendered policy |
