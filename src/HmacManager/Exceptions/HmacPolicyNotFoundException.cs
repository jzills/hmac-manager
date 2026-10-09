namespace HmacManager.Exceptions;

/// <summary>
/// A class representing an exception that occurs due to a missing policy.
/// </summary>
public class HmacPolicyNotFoundException : Exception
{
    /// <summary>
    /// Creates an instance of <see cref="HmacPolicyNotFoundException"/>.
    /// </summary>
    /// <param name="message">An optional error message.</param>
    /// <param name="policy">An optional policy.</param>/// 
    public HmacPolicyNotFoundException(string policy, string? message = null) 
        : base(message ?? $"The specified policy with name \"{policy}\" could not be found.")
    {
    }
}