using HmacManager.Caching;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Records that <c>AddNonceCache</c> replaced the built-in cache for <see cref="CacheType"/>.
/// </summary>
/// <remarks>
/// Registered as an instance beside the <see cref="NonceCacheRegistration"/>, so the replacement can be
/// known without constructing the cache: that is created the first time a request needs it.
/// </remarks>
internal sealed record NonceCacheReplacement(NonceCacheType CacheType);
