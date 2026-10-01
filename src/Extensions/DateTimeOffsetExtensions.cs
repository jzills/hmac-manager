namespace HmacManager.Extensions;

/// <summary>
/// Provides extension methods for <see cref="DateTimeOffset"/>.
/// </summary>
internal static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Determines whether the <paramref name="dateRequested"/> is within a valid time range based on the maximum age.
    /// </summary>
    /// <param name="dateRequested">The <see cref="DateTimeOffset"/> representing the requested date and time.</param>
    /// <param name="maxAgeInSeconds">The maximum allowable age in seconds for the request to be considered valid.</param>
    /// <returns><c>true</c> if the <paramref name="dateRequested"/> is within the specified maximum age; otherwise, <c>false</c>.</returns>
    public static bool HasValidDateRequested(
      this DateTimeOffset dateRequested,
      int maxAgeInSeconds
    ) => dateRequested.HasValidDateRequested(maxAgeInSeconds, TimeProvider.System);

    internal static bool HasValidDateRequested(
      this DateTimeOffset dateRequested,
      int maxAgeInSeconds,
      TimeProvider clock
    )
    {
        TimeSpan elapsed = clock.GetUtcNow().Subtract(dateRequested);
        return elapsed >= TimeSpan.Zero
            && elapsed < TimeSpan.FromSeconds(maxAgeInSeconds);
    }
}