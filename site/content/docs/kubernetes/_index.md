---
title: Kubernetes
description: The ext-authz verifier, the HmacPolicy CRD, the Helm chart and enforcement.
weight: 4
---

HmacManager runs in a cluster as an [Envoy ext-authz](https://www.envoyproxy.io/docs/envoy/latest/api-v3/extensions/filters/http/ext_authz/v3/ext_authz.proto)
HTTP server. Istio calls it before forwarding a request, so the check happens
outside your application and needs no change to it.

{{< hm-flow >}}

Three pieces are deployed together by one chart: the **verifier** that answers
ext-authz checks, the **operator** that turns `HmacPolicy` resources into the
config the verifier mounts, and **Redis** for replay protection.

{{< hm-children >}}
