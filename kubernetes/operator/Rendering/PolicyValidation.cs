using HmacManager.Policies;

namespace HmacManager.Operator.Rendering;

/// <summary>
/// Validates a policy using the library's own rules rather than a re-implementation, by running
/// it through the public <see cref="HmacPolicyCollection.Add"/> path (the same validators the
/// pods enforce). This keeps "what is a valid policy" defined in exactly one place.
/// </summary>
public static class PolicyValidation
{
    /// <returns>
    /// <c>IsValid</c> and, when invalid, a human-readable <c>Message</c> suitable for the
    /// resource's <c>.status</c>.
    /// </returns>
    public static (bool IsValid, string? Message) Validate(string name, string publicKey, string? privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return (false, "No private key available: privateKeySecretRef is unset or the referenced Secret/key was not found.");
        }

        if (!Guid.TryParse(publicKey, out var parsedPublicKey))
        {
            return (false, $"publicKey \"{publicKey}\" is not a valid GUID.");
        }

        try
        {
            new HmacPolicyCollection().Add(new HmacPolicy(name)
            {
                Keys = new KeyCredentials { PublicKey = parsedPublicKey, PrivateKey = privateKey },
            });

            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }
}
