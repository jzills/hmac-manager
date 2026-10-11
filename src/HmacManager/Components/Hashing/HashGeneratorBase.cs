using System.Security.Cryptography;
using System.Text;

namespace HmacManager.Components;

/// <summary>
/// Provides functionality to compute a hash for a given content.
/// </summary>
public abstract class HashGeneratorBase
{
    /// <summary>
    /// Computes the hash of the provided content asynchronously and returns the result as a Base64-encoded string.
    /// </summary>
    /// <param name="algorithm">The hash algorithm used on the input content.</param> 
    /// <param name="content">The input string to hash.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the Base64-encoded hash string.</returns>
    public async Task<string> ExecuteAsync(HashAlgorithm algorithm, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var hashBytes = await algorithm.ComputeHashAsync(stream);
        return Convert.ToBase64String(hashBytes);
    }
}