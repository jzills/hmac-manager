using System.Security.Cryptography;
using HmacManager.Exceptions;

namespace HmacManager.Components;

/// <summary>
/// A class representing a <see cref="SignatureHashGenerator"/>.
/// </summary>
public sealed class SignatureHashGenerator : HashGeneratorBase, IHashGenerator
{
    /// <summary>
    /// Stores the private key used for signing operations. This value may be <c>null</c>.
    /// </summary>
    private readonly string _privateKey;

    /// <summary>
    /// Specifies the algorithm used for signing hashes.
    /// </summary>
    private readonly SigningHashAlgorithm _signingHashAlgorithm;

    /// <summary>
    /// Creates a <see cref="SignatureHashGenerator"/> object.
    /// </summary>
    /// <param name="privateKey"><c>string?</c></param>/// 
    /// <param name="signingHashAlgorithm"><see cref="SigningHashAlgorithm"/></param>
    /// <returns>A <see cref="SignatureHashGenerator"/> object.</returns>
    public SignatureHashGenerator(
        string privateKey, 
        SigningHashAlgorithm signingHashAlgorithm
    ) => (_privateKey, _signingHashAlgorithm) = (privateKey, signingHashAlgorithm);

    /// <inheritdoc/>
    public Task<string> HashAsync(string content)
    {
        var keyBytes = Convert.FromBase64String(_privateKey);
        using HashAlgorithm hashAlgorithm = _signingHashAlgorithm switch
        {
            SigningHashAlgorithm.HMACSHA1   => new HMACSHA1  (keyBytes),
            SigningHashAlgorithm.HMACSHA256 => new HMACSHA256(keyBytes),
            SigningHashAlgorithm.HMACSHA512 => new HMACSHA512(keyBytes),
            _                               => throw new HashAlgorithmNotSupportedException(_signingHashAlgorithm)
        };

        return ExecuteAsync(hashAlgorithm, content);
    }
}