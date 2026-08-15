using System.Text;
using HmacManager.Policies;

namespace Api;

/// <summary>
/// Stands in for whatever a real application resolves policies from — a
/// database, a secrets store, a tenant lookup. Registered as scoped, so a new
/// one is created per request and <see cref="GetPolicies"/> runs per request.
/// </summary>
public class PolicyStore(ILogger<PolicyStore> logger)
{
    public IHmacPolicyCollection GetPolicies()
    {
        // Log so you can watch this happen on every request rather than once
        // at startup, which is the entire difference from the baseline sample.
        logger.LogInformation("Loading HMAC policies for this request");

        var policies = new HmacPolicyCollection();

        var builder = new HmacPolicyBuilder("MyPolicy");
        builder.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));
        builder.UsePrivateKey(Convert.ToBase64String(Encoding.UTF8.GetBytes("thisIsMySuperCoolPrivateKey")));
        builder.UseMemoryCache(30);
        builder.AddScheme("RequireAccountAndEmail", scheme =>
        {
            scheme.AddHeader("X-Account");
            scheme.AddHeader("X-Email");
        });

        policies.Add(builder.Build());

        return policies;
    }
}
