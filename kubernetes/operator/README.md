# hmac-manager-operator

Kubernetes controller that reconciles `HmacPolicy` custom resources
(`hmac-manager.io/v1alpha1`) into the aggregate ConfigMap and Secret that the
[hmac-manager](https://hub.docker.com/r/zills/hmac-manager) verifier pods mount.

The `HmacPolicy` custom resource is the single source of truth for a policy's
public key, private-key Secret reference, hash algorithms, nonce window, and
schemes. This operator watches those resources in its namespace and keeps the
mounted configuration in sync, so policy and key changes take effect without a
pod restart.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/kubernetes/hmacpolicy-crd/)**

## Deployment

Deployed automatically by the
[hmac-manager Helm chart](https://jzills.github.io/hmac-manager/docs/kubernetes/helm-chart/);
it is not intended to be run standalone. The chart wires the namespaced RBAC
and the `Operator__*` configuration the controller needs.

| Env var | Purpose |
|---|---|
| `Operator__WatchNamespace` | Namespace to watch for `HmacPolicy` resources and write the aggregate ConfigMap/Secret into. |
| `Operator__ConfigMapName` | Name of the aggregate ConfigMap the verifier pods mount. |
| `Operator__SecretName` | Name of the aggregate Secret the verifier pods mount. |
| `Operator__NonceCacheType` | `Memory` or `Distributed` — applied to every rendered policy. |

## Status

Each reconciled resource gets a `.status`, so a policy that cannot be applied
reports itself rather than failing later at request time:

```bash
kubectl get hmacpolicies -n hmac-system
kubectl describe hmacpolicy my-policy -n hmac-system
```

`.status.phase` is `Ready` or `Invalid`, and `.status.message` says why —
a missing Secret, an unparseable key, an algorithm outside the permitted set.

See [the HmacPolicy CRD](https://jzills.github.io/hmac-manager/docs/kubernetes/hmacpolicy-crd/)
for the full spec, key rotation behaviour, and the Helm-versus-kubectl
ownership rule.

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)

## Releases

### v0.1.0

Initial release. A KubeOps-based controller that reconciles `HmacPolicy` custom
resources (`hmac-manager.io/v1alpha1`) into the aggregate ConfigMap and Secret the
verifier pods mount, keeping policy and key changes in sync without a pod restart.
Published as `zills/hmac-manager-operator:0.1.0` and deployed by the hmac-manager
Helm chart.
