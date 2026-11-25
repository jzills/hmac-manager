---
title: Metrics and the Grafana dashboard
linkTitle: Metrics
description: Prometheus metrics from the ext-authz service, a ServiceMonitor, and a Grafana dashboard built into the chart.
weight: 6
---

The ext-authz service can serve Prometheus metrics: every verification by
outcome and policy, verification latency, and ASP.NET Core's own request
metrics for the check as a whole. The chart can also add a `ServiceMonitor`
for the Prometheus Operator and **a ready-made Grafana dashboard**, so
monitoring is three values away.

![The HmacManager dashboard in Grafana: verifications per second, the share refused, replays, verify p95 and errors across the top; verifications by outcome, refusals by policy, verified requests by policy and verification latency below; ext-authz latency and responses along the bottom.](dashboard.png)

## Turning it on

All of it is off by default. The metrics port is reachable from anywhere the
Service is, and the series name every policy and how much traffic it carries,
so turning it on is a deliberate choice.

```yaml
metrics:
  enabled: true          # serve /metrics on port 9464 and add it to the Service
  serviceMonitor:
    enabled: true        # have the Prometheus Operator scrape it
  dashboard:
    enabled: true        # ship the dashboard as a ConfigMap for Grafana's sidecar
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

The dashboard needs `metrics.enabled` too: on its own it renders, but has
nothing to show.

## With kube-prometheus-stack

[kube-prometheus-stack](https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack)
runs everything the values above rely on: the Prometheus Operator, Grafana,
and Grafana's dashboard sidecar. Two of its defaults decide whether the
`ServiceMonitor` and the dashboard are picked up, and both are easy to miss:

- **Prometheus only selects `ServiceMonitor`s labelled with its release**,
  `release: <its release name>`. Give ours that label.
- **The dashboard sidecar only looks in its own namespace.** Create the
  ConfigMap there.

With kube-prometheus-stack installed as `kps` in `monitoring`:

```yaml
metrics:
  enabled: true
  serviceMonitor:
    enabled: true
    labels:
      release: kps
  dashboard:
    enabled: true
    namespace: monitoring
```

If your stack is already configured to select every `ServiceMonitor`
(`prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues: false`)
or to search every namespace for dashboards
(`grafana.sidecar.dashboards.searchNamespace: ALL`), the corresponding line
is not needed.

## Checking it works

From the scrape outwards:

```bash
# 1. The service serves the metrics. Expect hmac_manager_verifications_total once a request is checked.
kubectl port-forward -n hmac-system svc/hmac-manager 9464
curl -s localhost:9464/metrics | grep '^hmac_manager'

# 2. Prometheus selected the ServiceMonitor: Status → Targets lists hmac-manager, state UP.

# 3. Grafana loaded the dashboard: search for "HmacManager", or check the sidecar's log.
kubectl logs -n monitoring deploy/kps-grafana -c grafana-sc-dashboard | grep hmac-manager
```

| Symptom | Cause |
| --- | --- |
| No **HmacManager** dashboard in Grafana | The sidecar is not watching the ConfigMap's namespace, or watches for a different label. Set `metrics.dashboard.namespace` or `metrics.dashboard.labels` to match it. |
| The dashboard is there, every panel says *No data* | Prometheus is not scraping the service: `metrics.enabled` is off, or the `ServiceMonitor` lacks the label your Prometheus selects by. |
| The **Job** and **Policy** filters are empty | The same, or nothing has been verified yet: the series appear on the first request checked. |

Without kube-prometheus-stack, scrape port `metrics` on the `hmac-manager`
Service however your Prometheus is configured, and import
`dashboards/hmac-manager.json` from the chart into Grafana, then pick your
Prometheus in its **Data source** filter.

## Why a port of its own

Envoy sends the ext-authz service the original request's method and path, and
treats a `200` as *allow*. A `/metrics` route on the verify port would answer
`200` to a request for `/metrics` on any protected service, and the gateway or
waypoint would let it through unsigned. So `/metrics` is served only on the
metrics port. On the verify port it is verified like any other path, and the
chart refuses a `metrics.port` equal to `service.port`. The development
signing helper on port 8081 is kept off the verify port for the same reason.

## The dashboard

The dashboard is `dashboards/hmac-manager.json` in the chart. It shows:

- **Verifications per second, the share refused, replays, p95 latency and
  errors**, as a row of stats. Replays and errors turn red above zero.
- **Verifications by outcome**, stacked: `verified` and every reason for a
  refusal, including `unsigned` for requests with no HMAC headers at all.
- **Refusals by policy**, and **verified requests by policy**. A refusal from
  before a policy resolved (`unsigned`, `invalid_headers`, `unknown_policy`)
  has no policy, deliberately: that name came from the caller.
- **Verification latency** p50, p95 and p99, and the same for **the ext-authz
  check as a whole**, which is what the mesh waits on for each request.
  Both histograms run from 0.1 ms to 5 s; a verification against Redis
  typically takes about a millisecond, and against the in-process cache a
  fraction of one.
- **ext-authz responses** by status: `200` allows, `403` denies, and a `5xx`
  is the check itself failing. These panels count only the checks: the
  scrape, and the development signing helper, are left out.

Filter by `job` and by policy at the top.

**Errors** above zero almost always means [Redis](../redis/) is unreachable.
The service fails closed, so those requests are refused, not let through. On
the dashboard an outage looks like this: errors and `5xx` responses rise, and
verification latency climbs to the 5 s Redis timeout, until Redis is back.

[The metrics reference](../../reference/metrics/) lists every series and
outcome.
