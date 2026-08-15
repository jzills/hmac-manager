---
title: Dynamic policies
description: Changing the policy set at runtime, or resolving it per request from a store.
weight: 6
---

Policies do not have to be fixed at startup. There are two mechanisms, and they
are not interchangeable.

## Mutating the singleton collection

`IHmacPolicyCollection` is registered as a singleton by both `AddHmacManager`
and `AddHmac`, and can be injected and modified.

```csharp
public class PolicyAdmin(IHmacPolicyCollection policies)
{
    public void Add(HmacPolicy policy) => policies.Add(policy);
    public void Remove(string name)    => policies.Remove(name);
}
```

{{% hm-note kind="warn" %}}
This is in-memory only. Changes do not survive a restart and do not propagate
to other instances — each process has its own singleton. If you need either,
you need a backing store, and then you want the scoped collection below.
{{% /hm-note %}}

## Scoped policies — resolved per request

`EnableScopedPolicies` replaces the collection with a delegate that runs per
request, with the `IServiceProvider` available. This is the mechanism for
policies that live in a database.

```csharp
builder.Services.AddHmacManager(options =>
{
    options.EnableScopedPolicies(serviceProvider =>
    {
        var db = serviceProvider.GetRequiredService<PolicyDbContext>();
        var policies = new HmacPolicyCollection();

        foreach (var record in db.Policies)
        {
            var builder = new HmacPolicyBuilder(record.Name);
            builder.UsePublicKey(record.PublicKey);
            builder.UsePrivateKey(record.PrivateKey);
            builder.UseDistributedCache(maxAgeInSeconds: 300);

            policies.Add(builder.Build());
        }

        return policies;
    });
});
```

Two things to know:

**It overrides everything else.** With scoped policies enabled, any policy
configured through `AddPolicy` or an `IConfigurationSection` is ignored. The
delegate is the whole policy set.

**It runs on every authenticating request.** If that delegate hits a database,
so does every request. There is no built-in caching layer, so if the store is
remote you should add one — an `IMemoryCache` around the lookup inside the
delegate is usually enough.

## Reloading from configuration

For the common case of "policies live in configuration and configuration
changes", neither mechanism is needed. A configuration provider that supports
reloading already propagates to the live policy set — see
[configuration binding](../configuration-binding/). The reload is reported at
`Information`, and a reload that fails keeps the previous set rather than
leaving the application with none.

## Which to use

| Need | Use |
| --- | --- |
| Policies change with configuration | [configuration binding](../configuration-binding/) |
| A policy added or revoked at runtime, one instance, no persistence | the singleton collection |
| Policies in a database, or per-tenant | `EnableScopedPolicies` |
| Policies as Kubernetes resources | [the `HmacPolicy` CRD](../../kubernetes/hmacpolicy-crd/) |
