---
title: Log events
description: Every event id the library emits, with its level.
weight: 2
---

Event ids are stable across releases. **An id is never reused for a different
event**, so it is safe to alert on one.

| Id | Level | Event |
| --- | --- | --- |
| 1000 | `Debug` | Request signed |
| 1001 | `Warning` | Signing produced no HMAC |
| 1002 | `Trace` | Signing content computed |
| 1003 | `Warning` | Outgoing request not sent |
| 1100 | `Debug` | Request verified |
| 1101 | `Warning` | Scheme headers missing |
| 1102 | `Warning` | Request outside the max-age window |
| 1103 | `Warning` | Nonce replayed |
| 1104 | `Warning` | Signature mismatch |
| 1105 | `Trace` | Signature mismatch detail |
| 1200 | `Debug` | Policy not found |
| 1201 | `Warning` | Nonce cache not registered |
| 1210 | `Information` | Watching configuration for policy changes |
| 1211 | `Information` | Policies reloaded |
| 1212 | `Warning` | Policy reload failed, previous set retained |
| 1300 | `Debug` | No HMAC header; authentication skipped |
| 1301 | `Warning` | `OnValidateKeys` rejected the credentials |
| 1302 | `Debug` | Authentication succeeded |
| 1303 | `Warning` | Authentication failed |
| 1310 | `Debug` | Requested policy not registered |

## Ranges

| Range | Emitted by |
| --- | --- |
| 1000–1099 | Signing |
| 1100–1199 | Verification |
| 1200–1299 | Factory resolution and policy reloading |
| 1300–1399 | The authentication handler |

## Reading a rejection

Each failure mode has its own id, so the id alone says what went wrong:

| Seeing | Means |
| --- | --- |
| 1102 | Clocks disagree by more than the replay window, or the request really is old |
| 1103 | The same nonce twice — a genuine replay, or a client reusing one |
| 1104 | The two sides built different [signing content](../../concepts/signing-content/) |
| 1101 | A scheme header was missing from the request |
| 1310 / 1200 | The request named a policy this host does not have |
| 1300 | Not an HMAC request at all — no header, so authentication was skipped |

For 1104, turn on `Trace` and compare event 1002 on the signer against 1105 on
the verifier. See
[diagnosing a mismatch](../../dotnet/logging/#diagnosing-a-signature-mismatch).

{{% hm-note %}}
Caller-controlled rejections — 1300, 1310, 1200 — are `Debug`, not `Warning`.
An unauthenticated caller can produce them at will, and an edge deployment must
not be drivable to unbounded `Warning` volume by traffic anyone can send.
{{% /hm-note %}}

## Private keys

No message at any level carries a private key. A test asserts this over the
full sign/verify path with every level enabled.

Signing content (1002, 1105) is not secret — it travels with the request — but
it does contain the public key, the nonce and any scheme header values, which
may identify a user. That is why it is `Trace` and off by default.
