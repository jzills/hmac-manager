namespace HmacManager.Extensions;

/// <summary>
/// Provides extension methods for <see cref="KeyValuePair{TKey, TValue}"/> where the key is a string and the value is a nullable string.
/// </summary>
internal static class KeyValuePairExtensions
{
    /// <summary>
    /// Formats the <see cref="KeyValuePair{TKey, TValue}"/> as a string in the format "key=value".
    /// </summary>
    /// <param name="source">The <see cref="KeyValuePair{TKey, TValue}"/> to format.</param>
    /// <returns>A string representing the key-value pair in the format "key=value".</returns>
    internal static string FormatAsEquality(this KeyValuePair<string, string?> source) => $"{source.Key}={source.Value}";

    /// <summary>
    /// Determines whether the <see cref="KeyValuePair{TKey, TValue}"/> has both a non-empty key and a non-empty value.
    /// </summary>
    /// <param name="source">The <see cref="KeyValuePair{TKey, TValue}"/> to check.</param>
    /// <returns><c>true</c> if both the key and value are non-empty; otherwise, <c>false</c>.</returns>
    internal static bool NonEmpty(this KeyValuePair<string, string?> source) =>
        !string.IsNullOrWhiteSpace(source.Key) &&
        !string.IsNullOrWhiteSpace(source.Value);
}