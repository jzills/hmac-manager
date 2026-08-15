---
title: The ext-authz service
description: What the verifier does, its ports, environment and image.
weight: 1
---

`zills/hmac-manager` is an Envoy ext-authz HTTP server. The Istio waypoint
proxy or ingress gateway calls it before forwarding any inbound request; a
valid HMAC signature passes, anything else is rejected with `403 Forbidden`.

The check runs against the original method and path, which the gateway passes
through in the ext-authz call — the verifier rebuilds the same
[signing content](../../concepts/signing-content/) the client signed.

## Ports

| Port | Purpose |
| --- | --- |
| `8080` | ext-authz check endpoint. This is the one the Service exposes. |
| `8081` | Signing helper. Only active when `environment: Development`, never exposed by the Service. |

## Image

| Tag | Meaning |
| --- | --- |
| `latest` | Most recent release |
| `X.Y.Z` | A specific release |

Pin a version in production — [`zills/hmac-manager`](https://hub.docker.com/r/zills/hmac-manager)
on Docker Hub.

## Configuration

Under Helm, everything below is set for you — see
[chart values](../../reference/chart-values/), in particular `logging.level`
and `environment`. The rest of this section is for running the image directly.

| Variable | Required | Description |
| --- | --- | --- |
| `ConnectionStrings__Redis` | no | Redis connection string. When set, enables the shared distributed nonce cache for multi-replica deployments. |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` (default) or `Development`. `Development` activates the signing helper on port 8081. |
| `ASPNETCORE_URLS` | no | Listening URL. Defaults to `http://+:8080`. |
| `SignPort` | no | Port for the dev-only signing helper. Defaults to `8081`. |

Policies come from a JSON file mounted at `/etc/hmac-manager/config.json`,
which is the same shape as the
[configuration schema](../../reference/configuration-schema/) the .NET library
binds:

```json
{
  "HmacManager": [
    {
      "Name": "my-policy",
      "Keys": {
        "PublicKey": "00000000-0000-0000-0000-000000000001"
      },
      "Algorithms": {
        "ContentHashAlgorithm": "SHA256",
        "SigningHashAlgorithm": "HMACSHA256"
      },
      "Nonce": {
        "CacheType": "Distributed",
        "MaxAgeInSeconds": 60
      }
    }
  ]
}
```

{{% hm-note kind="warn" %}}
Private keys are injected separately, as environment variables
(`HmacManager__0__Keys__PrivateKey` and so on), and must never be written into
the config file. Under Helm the
[operator](../hmacpolicy-crd/) maintains both the file and those variables from
`HmacPolicy` resources, sourcing keys from Secrets.
{{% /hm-note %}}

## Development signing endpoint

Setting `environment: Development` activates a `/sign` helper on port 8081.
It produces a valid signature for a request so you can exercise enforcement
without writing a client first. It is not exposed by the Service, so reaching
it means port-forwarding:

```bash
kubectl port-forward deploy/hmac-manager 9090:8081 -n hmac-system

curl -s -X POST http://localhost:9090/sign \
  -H "Content-Type: application/json" \
  -d '{"policy":"my-policy","method":"GET","uri":"http://echo.default.svc.cluster.local/"}'
```

`policy`, `method` and `uri` are required. `body` is optional — omit it to sign
a request with no body, such as a `GET`. The response is the HMAC headers to
attach to your request.

{{% hm-note kind="warn" %}}
This endpoint signs with the policy's private key on request, for anyone who
can reach it. Never set `environment: Development` in a production cluster.
{{% /hm-note %}}

## Signing from a client

Any of the three surfaces produce signatures this service accepts — the
[.NET library](../../dotnet/), the
[TypeScript client](../../client/), or the helper above.
