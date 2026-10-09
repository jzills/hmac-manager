using System.Security.Cryptography;
using HmacManager.Exceptions;

namespace HmacManager.Components;

/// <summary>
/// A class representing a <see cref="ContentHashGenerator"/>.
/// </summary>
public sealed class ContentHashGenerator : HashGeneratorBase, IHashGenerator
{
    /// <summary>
    /// Specifies the algorithm used for computing content hashes.
    /// </summary>
    private readonly ContentHashAlgorithm _contentHashAlgorithm;

    /// <summary>
    /// Creates a <see cref="ContentHashGenerator"/> object.
    /// </summary>
    /// <param name="contentHashAlgorithm"><see cref="ContentHashAlgorithm"/></param>
    /// <returns>A <see cref="ContentHashGenerator"/> object.</returns>
    public ContentHashGenerator(ContentHashAlgorithm contentHashAlgorithm) => _contentHashAlgorithm = contentHashAlgorithm;

    /// <inheritdoc/>
    public Task<string> HashAsync(string content)
    {
        using HashAlgorithm hashAlgorithm = _contentHashAlgorithm switch
        {
            ContentHashAlgorithm.SHA1   => SHA1  .Create(),
            ContentHashAlgorithm.SHA256 => SHA256.Create(),
            ContentHashAlgorithm.SHA512 => SHA512.Create(),
            _                           => throw new HashAlgorithmNotSupportedException(_contentHashAlgorithm)
        };

        return ExecuteAsync(hashAlgorithm, content);
    }
}