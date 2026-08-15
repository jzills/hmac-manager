# Json configuration

[Simple authentication](../WebToApiAuthentication/) with the policy declared in
`appsettings.json` instead of code. Diff the two `Program.cs` files and the
configuration binding is the whole difference.

```bash
dotnet run --project Api    # http://localhost:5110
dotnet run --project Web    # http://localhost:5111  (second terminal)
```

Then open <http://localhost:5111>.

## What to look at

| File | Why |
|---|---|
| [`Api/appsettings.json`](Api/appsettings.json) | The policy, including a `ClaimType` per scheme header |
| [`Api/Program.cs`](Api/Program.cs) | `AddHmac(section)` — three lines, no policy in code |
| [`Web/appsettings.json`](Web/appsettings.json) | The signing side of the same policy |

The section name is arbitrary. What matters is that it binds to an **array** of
policies:

```csharp
builder.Services
    .AddAuthentication()
    .AddHmac(builder.Configuration.GetSection("HmacPolicies"));
```

## Claims without event handlers

The baseline sample builds its claims in `OnAuthenticationSuccessAsync`. Here
each header carries a `ClaimType` instead, and the authentication handler turns
the header values into claims directly — so this sample registers no
`HmacEvents` at all and the `POST` response still comes back with the account
and email on it.

Only the verifying end needs them. `Web/appsettings.json` lists the same headers
with no `ClaimType`, because signing only needs to know which headers are
covered.

## Notes

Invalid values — a `PublicKey` that is not a GUID, an unknown algorithm — fail
when the policy is built at startup, not on the first request.

`PrivateKey` is a shared secret and `appsettings.json` is committed here so the
sample runs with no setup. In a real application let configuration composition
supply it from user secrets, an environment variable or a secret store.

**📖 [Configuration binding](https://jzills.github.io/hmac-manager/docs/dotnet/configuration-binding/)**
· [Full schema](https://jzills.github.io/hmac-manager/docs/reference/configuration-schema/)
