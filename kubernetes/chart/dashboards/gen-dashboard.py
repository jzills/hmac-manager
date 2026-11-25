#!/usr/bin/env python3
"""Writes dashboards/hmac-manager.json, the Grafana dashboard the chart ships.

Run by hand from kubernetes/chart and commit the output; nothing in the build runs it. Edit the
dashboard here rather than in the JSON, so the queries stay readable and consistent.

The metric and label names are the library's contract, from
src/HmacManager/Diagnostics/HmacMetrics.cs as a Prometheus exporter renders them; the reference is
site/content/docs/reference/metrics.md.
"""
import json
import pathlib

DS = {"type": "prometheus", "uid": "${datasource}"}
SEL = 'job=~"$job"'
VERIFICATIONS = "hmac_manager_verifications_total"
DURATION = "hmac_manager_verification_duration_seconds_bucket"
HTTP = "http_server_request_duration_seconds"

_ids = iter(range(1, 1000))


def target(expr, legend="", ref="A"):
    return {"datasource": DS, "expr": expr, "legendFormat": legend, "refId": ref, "range": True}


def rate(extra=""):
    return f"sum(rate({VERIFICATIONS}{{{SEL}{extra}}}[$__rate_interval]))"


def quantiles(metric, extra=""):
    return [
        target(
            f"histogram_quantile({q}, sum by (le) (rate({metric}{{{SEL}{extra}}}[$__rate_interval])))",
            f"p{round(q * 100)}",
            ref,
        )
        for q, ref in ((0.5, "A"), (0.95, "B"), (0.99, "C"))
    ]


def thresholds(*steps):
    return {"mode": "absolute", "steps": [{"color": "green", "value": None}]
            + [{"color": color, "value": value} for value, color in steps]}


def stat(title, expr, x, w, unit, steps=(), description=""):
    return {
        "id": next(_ids), "type": "stat", "title": title, "description": description,
        "datasource": DS, "gridPos": {"h": 4, "w": w, "x": x, "y": 0},
        "targets": [target(expr)],
        "options": {
            "reduceOptions": {"calcs": ["lastNotNull"], "fields": "", "values": False},
            "colorMode": "value", "graphMode": "area", "textMode": "auto",
        },
        "fieldConfig": {"defaults": {"unit": unit, "thresholds": thresholds(*steps)}, "overrides": []},
    }


def timeseries(title, targets, x, y, unit="reqps", stack=False, description=""):
    return {
        "id": next(_ids), "type": "timeseries", "title": title, "description": description,
        "datasource": DS, "gridPos": {"h": 8, "w": 12, "x": x, "y": y}, "targets": targets,
        "options": {
            "legend": {"displayMode": "table", "placement": "right", "calcs": ["mean", "max"]},
            "tooltip": {"mode": "multi", "sort": "desc"},
        },
        "fieldConfig": {"defaults": {"unit": unit, "custom": {
            "fillOpacity": 15 if stack else 0, "lineWidth": 1,
            "stacking": {"mode": "normal" if stack else "none", "group": "A"},
        }}, "overrides": []},
    }


ANY_ERROR = ((0.000001, "red"),)
REFUSED = ',hmac_manager_outcome!="verified"'
POLICY = ',hmac_manager_policy=~"$policy"'

panels = [
    stat("Verifications/s", rate(), 0, 5, "reqps",
         description="Every request the service was asked to verify, whatever the outcome."),
    stat("Refused", f"{rate(REFUSED)} / {rate()}", 5, 5, "percentunit", ((0.05, "orange"), (0.25, "red")),
         "Share of requests refused, for any reason."),
    stat("Replays/s", rate(',hmac_manager_outcome="replayed"'), 10, 5, "reqps", ANY_ERROR,
         "Requests whose nonce was already claimed: an attack, or a client resending a signed request verbatim."),
    stat("Verify p95",
         f"histogram_quantile(0.95, sum by (le) (rate({DURATION}{{{SEL}}}[$__rate_interval])))", 15, 5, "s",
         description="95th percentile time to verify a request, nonce claim included."),
    stat("Errors/s", rate(',hmac_manager_outcome="error"'), 20, 4, "reqps", ANY_ERROR,
         "Verifications that threw, almost always the nonce cache (Redis) being unreachable. "
         "Requests are refused while this is above zero."),
    timeseries("Verifications by outcome",
               [target(f"sum by (hmac_manager_outcome) (rate({VERIFICATIONS}{{{SEL}}}[$__rate_interval]))",
                       "{{hmac_manager_outcome}}")],
               0, 4, stack=True,
               description="verified was accepted; every other outcome was refused."),
    timeseries("Refusals by policy",
               [target(f"sum by (hmac_manager_policy, hmac_manager_outcome) "
                       f"(rate({VERIFICATIONS}{{{SEL}{REFUSED}{POLICY}}}[$__rate_interval]))",
                       "{{hmac_manager_policy}} · {{hmac_manager_outcome}}")],
               12, 4,
               description="Refusals before a policy resolved (unsigned, invalid_headers, unknown_policy) carry no "
                           "policy label, deliberately: that name is the caller's."),
    timeseries("Verified by policy",
               [target(f"sum by (hmac_manager_policy) (rate({VERIFICATIONS}"
                       f'{{{SEL},hmac_manager_outcome="verified"{POLICY}}}[$__rate_interval]))',
                       "{{hmac_manager_policy}}")],
               0, 12, stack=True),
    timeseries("Verification latency", quantiles(DURATION, POLICY), 12, 12, unit="s"),
    timeseries("ext-authz HTTP latency", quantiles(f"{HTTP}_bucket"), 0, 20, unit="s",
               description="The whole ext-authz check as ASP.NET Core saw it, body buffering included: "
                           "what the gateway or waypoint waits on for every request."),
    timeseries("ext-authz responses",
               [target(f"sum by (http_response_status_code) (rate({HTTP}_count{{{SEL}}}[$__rate_interval]))",
                       "{{http_response_status_code}}")],
               12, 20, stack=True,
               description="200 allows, 403 denies. A 5xx is the check itself failing, which the mesh "
                           "handles according to its failure-mode setting."),
]


def variable(name, label, query):
    return {
        "name": name, "label": label, "type": "query", "datasource": DS,
        "query": {"query": query, "refId": name}, "definition": query,
        "refresh": 2, "includeAll": True, "multi": True, "allValue": ".*", "current": {}, "hide": 0,
    }


dashboard = {
    "title": "HmacManager",
    "uid": "hmac-manager",
    "description": "Request verification by the HmacManager ext-authz service: outcomes, refusals by policy, latency.",
    "tags": ["hmac-manager"],
    "schemaVersion": 39, "version": 1, "editable": True, "graphTooltip": 1,
    "time": {"from": "now-1h", "to": "now"}, "refresh": "30s",
    "templating": {"list": [
        {"name": "datasource", "label": "Data source", "type": "datasource", "query": "prometheus",
         "current": {}, "hide": 0},
        variable("job", "Job", f"label_values({VERIFICATIONS}, job)"),
        variable("policy", "Policy", f'label_values({VERIFICATIONS}{{job=~"$job"}}, hmac_manager_policy)'),
    ]},
    "annotations": {"list": []},
    "panels": panels,
}

out = pathlib.Path(__file__).with_name("hmac-manager.json")
out.write_text(json.dumps(dashboard, indent=2) + "\n")
print(f"wrote {out} ({len(panels)} panels)")
