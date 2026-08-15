# Authorization policies

[Simple authentication](../WebToApiAuthentication/) with the requirement
expressed as an ASP.NET Core **authorization** policy instead of the
`[HmacAuthenticate]` attribute — so it composes with everything else
authorization can already do.

```bash
dotnet run --project Api    # http://localhost:5120
dotnet run --project Web    # http://localhost:5121  (second terminal)
```

Then open <http://localhost:5121>. You get two results:

```json
{ "get": { "status": 200, ... }, "delete": { "status": 403, "body": "" } }
```

That 403 is the sample. The Api holds two HMAC policies, `MyPolicy` and
`AdminPolicy`; the Web app only has `MyPolicy`'s key. Its request to the
admin-only endpoint is signed, verified and **authenticated** — and then refused,
because it is not the caller that endpoint is for. A forged or unsigned request
gets 401 instead; the two failures are distinct on purpose.

## What to look at

| File | Why |
|---|---|
| [`Api/Program.cs`](Api/Program.cs) | Two HMAC policies, then `AddAuthorization` with `RequireHmacAuthentication` |
| [`Api/Controllers/WeatherForecastController.cs`](Api/Controllers/WeatherForecastController.cs) | `[Authorize(Policy = ...)]` per action — no `[HmacAuthenticate]` anywhere |

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireHmacAuthentication("AdminPolicy", "RequireAccountAndEmail"));
});
```

`RequireHmacAuthentication` is shorthand for requiring the policy and scheme
claims the authentication handler adds. `RequireHmacPolicy` and
`RequireHmacScheme` are available separately, and both accept several names when
an endpoint should admit more than one caller.

Because these are ordinary authorization policies, they combine with the rest of
the framework — `RequireRole`, a custom requirement, or a default policy applied
with `RequireAuthorization()`.

## Notes

The keys are literals committed to this repository so the sample runs with no
setup. They are not an example of key handling.

**📖 [Authorization](https://jzills.github.io/hmac-manager/docs/dotnet/authorization/)**
