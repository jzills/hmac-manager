# HmacManager

[![NuGet Version](https://img.shields.io/nuget/v/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![NuGet Downloads](https://img.shields.io/nuget/dt/HmacManager.svg)](https://www.nuget.org/packages/HmacManager/) [![.NET](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml/badge.svg)](https://github.com/jzills/hmac-manager/actions/workflows/pr.yml)

HMAC request authentication for ASP.NET Core. Sign outgoing requests, verify
incoming ones, or both — against named policies, with replay protection built
in.

Targets `net8.0` and `net10.0`.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/dotnet/)**

## Install

```bash
dotnet add package HmacManager
```

## Verify incoming requests

`AddHmac` registers an authentication handler, so a request that fails
verification never reaches your endpoint.

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            policy.UsePrivateKey("zvg29s2cQ4idOqbUJWETOw==");
            policy.UseMemoryCache(maxAgeInSeconds: 300); // nonce / replay window
        });
    });

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

## Sign outgoing requests

Attach the handler to a named `HttpClient` and every request it sends is
signed.

```csharp
builder.Services
    .AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddHmacHttpMessageHandler("MyPolicy");
```

## Documentation

The full documentation lives at
**[jzills.github.io/hmac-manager](https://jzills.github.io/hmac-manager/docs/dotnet/)**:

| | |
| --- | --- |
| [Registration](https://jzills.github.io/hmac-manager/docs/dotnet/registration/) | `AddHmacManager` versus `AddHmac`, and schemes |
| [Authorization](https://jzills.github.io/hmac-manager/docs/dotnet/authorization/) | Requiring a specific policy or scheme per endpoint |
| [Configuration binding](https://jzills.github.io/hmac-manager/docs/dotnet/configuration-binding/) | Policies from an `IConfigurationSection` |
| [HttpClient](https://jzills.github.io/hmac-manager/docs/dotnet/http-client/) | Automatic signing, and why to prefer it |
| [Events](https://jzills.github.io/hmac-manager/docs/dotnet/events/) | `OnValidateKeys`, `OnAuthSuccess`, `OnAuthFailure` |
| [Dynamic policies](https://jzills.github.io/hmac-manager/docs/dotnet/dynamic-policies/) | Policies from a database, or changed at runtime |
| [Custom signing content](https://jzills.github.io/hmac-manager/docs/dotnet/custom-signing-content/) | Replacing the default signing string |
| [Logging](https://jzills.github.io/hmac-manager/docs/dotnet/logging/) | Categories, levels, and diagnosing a mismatch |

Concepts shared with the Kubernetes verifier and the TypeScript client —
[policies](https://jzills.github.io/hmac-manager/docs/concepts/policies/),
[schemes](https://jzills.github.io/hmac-manager/docs/concepts/schemes/),
[signing content](https://jzills.github.io/hmac-manager/docs/concepts/signing-content/),
[nonce and replay](https://jzills.github.io/hmac-manager/docs/concepts/nonce-and-replay/) —
are documented once, under
[Concepts](https://jzills.github.io/hmac-manager/docs/concepts/).

## Logging at a glance

Every component writes to `ILogger` with no configuration required, and falls
back to `NullLogger` when logging is not set up. Event ids are stable across
releases and are listed in the
[event reference](https://jzills.github.io/hmac-manager/docs/reference/log-events/).

```json
{
  "Logging": {
    "LogLevel": {
      "HmacManager": "Debug"
    }
  }
}
```

Private keys are never logged at any level, and a test asserts that over the
full sign/verify path with every level enabled.

## Source

[github.com/jzills/hmac-manager](https://github.com/jzills/hmac-manager)
