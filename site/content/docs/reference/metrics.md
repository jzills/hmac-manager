---
title: Metrics
description: Every instrument the library records, its tags, and every outcome.
weight: 2
---

HmacManager records metrics on a `System.Diagnostics.Metrics` meter named
**`HmacManager`** (`HmacManagerDiagnostics.MeterName`). Nothing is recorded
until something listens, so the library takes no dependency for it and an
application that collects nothing pays one enabled check per measurement.

Instrument and tag names are stable across releases, as [log event
ids](../log-events/) are. Names below are as the meter records them, then as a
Prometheus exporter renders them.

## Instruments

| Instrument | Prometheus | Type | Tags |
| --- | --- | --- | --- |
| `hmac_manager.verifications` | `hmac_manager_verifications_total` | Counter | `hmac_manager.outcome`, `hmac_manager.policy`, `hmac_manager.scheme` |
| `hmac_manager.verification.duration` | `hmac_manager_verification_duration_seconds` | Histogram, seconds | `hmac_manager.outcome`, `hmac_manager.policy`, `hmac_manager.scheme` |
| `hmac_manager.signings` | `hmac_manager_signings_total` | Counter | `hmac_manager.outcome`, `hmac_manager.policy`, `hmac_manager.scheme` |

The duration covers the whole of `VerifyAsync`, the nonce claim included, so
it is where a slow nonce store shows. It is recorded only for verifications
that reached a policy's `HmacManager`, not for requests refused before one.

## Verification outcomes

Every request the library is asked to verify is counted once, under exactly
one outcome.

| `hmac_manager.outcome` | Policy tag | Meaning |
| --- | --- | --- |
| `verified` | yes | Accepted. The nonce is claimed. |
| `invalid_headers` | only from `VerifyAsync` | The HMAC headers are absent or did not parse. |
| `unknown_policy` | **never** | The request named a policy this process does not have. |
| `unknown_scheme` | yes, no scheme | The policy exists but does not declare the scheme the request named. |
| `keys_rejected` | yes | `HmacEvents.OnValidateKeys` refused the policy's keys (ASP.NET Core handler only). |
| `headers_missing` | yes | A header the request's scheme signs is absent. |
| `expired` | yes | Outside the policy's `MaxAgeInSeconds` window. Usually clock skew. |
| `signature_mismatch` | yes | The recomputed signature differs: a wrong key, a client bug, or a forgery. [Reading a rejection](../log-events/#reading-a-rejection) covers telling them apart. |
| `replayed` | yes | The nonce was already claimed. |
| `error` | yes | Verification threw, almost always because the nonce cache could not be reached. The request is refused (the cache fails closed), so a store outage shows here rather than as a gap in traffic. |

Signing outcomes are `signed` and `failed`.

## Tags never carry what a caller chose

`hmac_manager.policy` and `hmac_manager.scheme` are set only once the name has
resolved to a policy or scheme this process has configured, so the number of
series is bounded by your configuration. A name that did not resolve came off
a request header: tagging it would let anyone who can reach the endpoint
create series without limit. That is why `unknown_policy` has no policy tag
and `unknown_scheme` has no scheme tag. No tag carries a nonce, a signature or
a key.

## Collecting them

- **ASP.NET Core with OpenTelemetry:** add the meter by name. See
  [Metrics](../../dotnet/metrics/).
- **Kubernetes:** the ext-authz service serves these on a Prometheus port of
  its own, and the chart can add a `ServiceMonitor` and a Grafana dashboard.
  See [Metrics](../../kubernetes/metrics/).
