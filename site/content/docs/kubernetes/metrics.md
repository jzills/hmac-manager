---
title: Metrics and the Grafana dashboard
linkTitle: Metrics
description: Prometheus metrics from the ext-authz service, a ServiceMonitor, and the dashboard.
weight: 6
---

The ext-authz service can serve Prometheus metrics: every verification by
outcome and policy, verification latency, and ASP.NET Core's own request
metrics for the check as a whole. The chart can also add a `ServiceMonitor`
for the Prometheus Operator and a Grafana dashboard.

All of it is off by default. The metrics port is reachable from anywhere the
Service is, and the series name every policy and how much traffic it carries,
so turning it on is a deliberate choice.

```yaml
metrics:
  enabled: true
  serviceMonitor:
    enabled: true
    labels:
      release: kube-prometheus-stack  # whatever your Prometheus selects by
  dashboard:
    enabled: true
```

| Value | Default | Meaning |
| --- | --- | --- |
| `metrics.enabled` | `false` | Serve `/metrics` on the metrics port, and add the port to the Service. |
| `metrics.port` | `9464` | The metrics port. Must differ from `service.port` and from 8081. |
| `metrics.serviceMonitor.enabled` | `false` | Render a `ServiceMonitor`. Needs the `monitoring.coreos.com` CRDs. |
| `metrics.serviceMonitor.interval` | `30s` | Scrape interval. |
| `metrics.serviceMonitor.labels` | `{}` | Extra labels, for your Prometheus's `serviceMonitorSelector`. |
| `metrics.dashboard.enabled` | `false` | Render the dashboard as a ConfigMap for Grafana's sidecar. |
| `metrics.dashboard.namespace` | release namespace | Where to create that ConfigMap. |
| `metrics.dashboard.labels` | `grafana_dashboard: "1"` | The label the sidecar watches for. |

## Why a port of its own

Envoy sends the ext-authz service the original request's method and path, and
treats a `200` as *allow*. A `/metrics` route on the verify port would answer
`200` to a request for `/metrics` on any protected service, and the gateway or
waypoint would let it through unsigned. So `/metrics` is served only on the
metrics port. On the verify port it is verified like any other path, and the
chart refuses a `metrics.port` equal to `service.port`. The development
signing helper on port 8081 is kept off the verify port for the same reason.

## The dashboard

The dashboard is `dashboards/hmac-manager.json` in the chart. With
`metrics.dashboard.enabled`, the chart puts it in a ConfigMap carrying the
label Grafana's dashboard sidecar watches for, which kube-prometheus-stack
runs by default. Without the sidecar, import the JSON from the chart directly.

It shows:

- **Verifications per second, the share refused, replays, p95 latency and
  errors**, as a row of stats.
- **Verifications by outcome**, stacked: `verified` and every reason for a
  refusal.
- **Refusals by policy**, and **verified requests by policy**.
- **Verification latency** p50, p95 and p99, and the same for **the ext-authz
  check as a whole**, which is what the mesh waits on for each request.
- **ext-authz responses** by status: `200` allows, `403` denies, and a `5xx`
  is the check itself failing.

Filter by `job` and by policy at the top. **Errors** above zero almost always
means [Redis](../redis/) is unreachable. The service fails closed, so those
requests are refused, not let through.

[The metrics reference](../../reference/metrics/) lists every series and
outcome.
