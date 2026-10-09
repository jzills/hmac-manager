namespace HmacManager.Components;

/// <summary>
/// Defines an interface for generating hash values.
/// </summary>
internal interface IHashGenerator
{
    /// <summary>
    /// Hashes the specified <c>string</c> content.
    /// </summary>
    /// <param name="content">A <c>string</c> representing the content to be hashed.</param>
    /// <returns>A hashed <c>string</c>.</returns>
    Task<string> HashAsync(string content);
}