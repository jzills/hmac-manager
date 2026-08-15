# HmacManager samples

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![NuGet Downloads](https://img.shields.io/nuget/dt/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml)

Each sample is a client signing requests to an API that verifies them. They
differ in **one** thing each, so the diff between two of them is the feature.

| Sample | Changes | Api | Client |
|---|---|---|---|
| [Simple authentication](./WebToApiAuthentication/README.md) | The baseline — start here | 5100 | 5101 |
| [Json configuration](./WebToApiAuthenticationWithJsonConfiguration/README.md) | Policies from `appsettings.json` | 5110 | 5111 |
| [Authorization policies](./WebToApiAuthenticationWithAuthorizationPolicies/README.md) | `[Authorize]` instead of `[HmacAuthenticate]` | 5120 | 5121 |
| [Scoped policies](./WebToApiAuthenticationWithScopedPolicies/README.md) | Policies resolved per request | 5130 | 5131 |
| [JavaScript client](./WebToApiAuthenticationWithJavaScriptClient/README.md) | The client is Node, not .NET | 5140 | — |
| [Node API](./NodeApiAuthentication/README.md) | The **API** is Node, called by Node and .NET | 5200 | — |

## Running one

Start the Api first, then the client, in a second terminal:

```bash
cd WebToApiAuthentication
dotnet run --project Api
dotnet run --project Web
```

Then open the client's URL from the table above.

The last two samples use the TypeScript client, which is linked from source, so
build it once first — `cd client/lib && npm ci && npm run build`. Their READMEs
have the steps. Use `npm install` in a sample, not `npm ci`: the dependency is a
`file:` link and no lockfile is committed.

Everything runs over plain HTTP on localhost, so no development certificate is
needed. HMAC authenticates a request and detects tampering; it does not encrypt
one, so a real deployment still belongs behind TLS.

## The keys

The keys are literals committed to this repository so the samples run with no
setup. Every sample deliberately uses the **same** key pair, so the only
difference between any two of them is the feature. They are not an example of
key handling — see
[configuration binding](https://jzills.github.io/hmac-manager/docs/dotnet/configuration-binding/)
for where a real key belongs.

Each sample is built, and the two Node ones are run end to end, by the
`Sample Builds` job in [`pr.yml`](../.github/workflows/pr.yml).

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/samples/)**
