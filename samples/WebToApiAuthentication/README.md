# Simple authentication

The baseline sample: a **Web** app signing requests to a protected **Api**. Every
other sample is this one with a single thing changed, so a diff against this
directory is the feature.

```bash
dotnet run --project Api    # http://localhost:5100
dotnet run --project Web    # http://localhost:5101  (second terminal)
```

Then open <http://localhost:5101>. The response is what the Api returned for a
signed `GET` and a signed `POST`.

## What to look at

| File | Why |
|---|---|
| [`Api/Program.cs`](Api/Program.cs) | `AddHmac` — the policy, its scheme, and the `HmacEvents` hooks |
| [`Api/Controllers/WeatherForecastController.cs`](Api/Controllers/WeatherForecastController.cs) | `[HmacAuthenticate]` — what actually enforces it |
| [`Web/Program.cs`](Web/Program.cs) | `AddHmacManager` + `AddHmacHttpMessageHandler` — the signing side |

The client side is one line. `AddHmacHttpMessageHandler` attaches a handler to a
named `HttpClient`, and from then on every request that client sends is signed:

```csharp
builder.Services
    .AddHttpClient("WeatherApi", client => client.BaseAddress = new Uri(apiUrl))
    .AddHmacHttpMessageHandler("MyPolicy", "RequireAccountAndEmail");
```

Nothing in the calling code mentions HMAC.

## Seeing it work

The `RequireAccountAndEmail` scheme folds `X-Account` and `X-Email` into the
signature, and the Api turns them into claims — which is why the `POST` response
echoes the account and email back. They cannot be altered in transit without
invalidating the signature.

Two things worth trying:

```bash
# Unsigned — 401.
curl -i http://localhost:5100/api/weatherforecast

# Comment out one of the header lines in Web/Program.cs and rerun.
# Signing fails: the signature covers a header that is not there.
```

## Notes

Both projects run over plain HTTP so the sample needs no development
certificate. HMAC authenticates a request and detects tampering; it does not
encrypt one, so a real deployment still belongs behind TLS.

The keys are literals committed to this repository so the sample runs with no
setup. They are not an example of key handling — see
[configuration binding](https://jzills.github.io/hmac-manager/docs/dotnet/configuration-binding/)
for where a real key belongs.

**📖 [Documentation](https://jzills.github.io/hmac-manager/docs/)**
