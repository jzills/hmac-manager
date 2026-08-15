# HmacManager samples

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![NuGet Downloads](https://img.shields.io/nuget/dt/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml)

Each sample is a web app signing requests to an API that verifies them. They
differ in one thing each, so the diff between two of them is the feature.

| Sample | Shows |
|---|---|
| [Simple authentication](./WebToApiAuthentication/README.md) | The baseline — `AddHmac` on the API, `AddHmacHttpMessageHandler` on the client |
| [Json configuration](./WebToApiAuthenticationWithJsonConfiguration/README.md) | Policies bound from an `IConfigurationSection` |
| [Authorization policies](./WebToApiAuthenticationWithAuthorizationPolicies/README.md) | Requiring a specific policy and scheme per endpoint |
| [JavaScript client](./WebToApiAuthenticationWithJavaScriptClient/README.md) | Browser-side signing with the npm package |
| [Scoped policies](./WebToApiAuthenticationWithScopedPolicies/) | Policies resolved per request |

Both projects must be running — start the API first:

```bash
cd WebToApiAuthentication
dotnet run --project Api
dotnet run --project Web    # in a second terminal
```

The keys in these samples are literals committed to the repository so they run
with no setup. They are not examples of key handling.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/samples/)**
