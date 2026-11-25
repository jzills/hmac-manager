---
title: Metrics
description: Counting verifications by outcome and timing them, with OpenTelemetry.
weight: 11
---

HmacManager counts every verification by its outcome, times it, and counts
every signing. It records on a `System.Diagnostics.Metrics` meter named
`HmacManager`, so there is nothing to register in the library: add the meter
to whatever collects your application's metrics.

With OpenTelemetry:

```csharp
using HmacManager.Diagnostics;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(HmacManagerDiagnostics.MeterName)
        .AddPrometheusExporter()); // or .AddOtlpExporter()
```

Until a listener subscribes, nothing is recorded and no dependency is
needed. `dotnet-counters monitor --counters HmacManager` shows them live
without changing the application at all.

## What you get

- **`hmac_manager.verifications`**: one count per request, tagged with the
  outcome (`verified`, `expired`, `replayed`, `signature_mismatch` and the
  rest) and the policy and scheme it was checked against.
- **`hmac_manager.verification.duration`**: how long verification took, nonce
  claim included.
- **`hmac_manager.signings`**: outgoing requests signed, and failures to sign.

[The metrics reference](../../reference/metrics/) lists every instrument, tag
and outcome.

## Where requests are counted

A request is counted once, wherever it ends:

- `HmacManager.VerifyAsync` counts everything from a parsed request onward,
  under the manager's own policy and scheme.
- `HmacAuthenticationHandler`, through `HmacAuthenticationContextProvider`,
  counts what is refused before a manager is reached: headers that do not
  parse, a policy this host does not have, a scheme the policy does not
  declare, and keys `OnValidateKeys` rejected.

A name from a request is only ever a tag once it has resolved to a configured
policy or scheme, so a caller cannot create series by inventing names. A
request naming an unknown policy is counted as `unknown_policy` with no policy
tag at all.

## Alongside logging

Metrics say how often; [logging](../logging/) says which request and why. The
outcomes map onto the verification log events: `expired` is event 1102,
`replayed` 1103, `signature_mismatch` 1104. A rise in one outcome on a
dashboard is the cue to look for its event in the logs.
