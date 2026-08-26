# Scoped policies

[Simple authentication](../WebToApiAuthentication/) with the Api resolving its
policies **per request** instead of fixing them at startup — the shape you need
when keys live in a database, a secret store or a per-tenant lookup.

```bash
dotnet run --project Api    # http://localhost:5130
dotnet run --project Web    # http://localhost:5131  (second terminal)
```

Then open <http://localhost:5131> and watch the Api's terminal:

```
info: Api.PolicyStore[0]
      Loading HMAC policies for this request
```

A line per load, as requests arrive — rather than once at startup. That is the
whole sample.

## What to look at

| File | Why |
|---|---|
| [`Api/Program.cs`](Api/Program.cs) | `EnableScopedPolicies` — the accessor, and nothing else changed |
| [`Api/PolicyStore.cs`](Api/PolicyStore.cs) | Stands in for the database or secret store |

```csharp
options.EnableScopedPolicies(serviceProvider =>
    serviceProvider.GetRequiredService<PolicyStore>().GetPolicies());
```

The accessor is handed a scoped `IServiceProvider`, so it can resolve anything
your container knows about — a `DbContext`, an `IHttpContextAccessor` to pick a
tenant from the request, a client for a secret store. A key rotated in the store
takes effect on the next request: no restart, no cache to invalidate.

The Web project is unchanged from the baseline. Only the verifying end resolves
policies dynamically, and the signing end cannot tell the difference.

## Notes

The accessor runs more than once per request — each component that needs the
policy set asks for it — so back it with something cheap, or cache within the
scope. A per-request round trip to a database on the authentication path is not
what you want.

The keys here are literals in `PolicyStore`, which rather defeats the purpose;
a real store would fetch them. They are not an example of key handling.

**📖 [Dynamic policies](https://jzills.github.io/hmac-manager/docs/dotnet/dynamic-policies/)**
