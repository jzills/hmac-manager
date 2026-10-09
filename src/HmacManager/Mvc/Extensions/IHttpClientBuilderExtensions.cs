using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HmacManager.Components;

namespace HmacManager.Mvc.Extensions;

/// <summary>
/// A class representing extension methods on an <see cref="IHttpClientBuilder"/> to add HMAC signing to HTTP requests.
/// </summary>
public static class IHttpClientBuilderExtensions
{
    /// <summary>
    /// Registers an <see cref="HttpMessageHandler"/> that handles creating an <see cref="HmacManager"/> through an implementation of <see cref="IHmacManagerFactory"/>. 
    /// Outgoing requests are then signed with the specified policy.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <param name="policy">The name of the registered policy to be used for signing the requests.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to further configure the client.</returns>
    /// <remarks>
    /// This method adds a <see cref="HttpMessageHandler"/> that automatically signs outgoing HTTP requests
    /// using the specified HMAC policy.
    /// </remarks>
    public static IHttpClientBuilder AddHmacHttpMessageHandler(
        this IHttpClientBuilder builder, 
        string policy
    ) => builder.AddHttpMessageHandler(provider => 
            GetDelegatingHandler(provider, policy));

    /// <summary>
    /// Registers an <see cref="HttpMessageHandler"/> that handles creating an <see cref="HmacManager"/> through an implementation of <see cref="IHmacManagerFactory"/>. 
    /// Outgoing requests are then signed with the specified policy and optional scheme.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <param name="policy">The name of the registered policy to be used for signing the requests.</param>
    /// <param name="scheme">The name of the registered scheme to be used for signing the requests.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> that can be used to further configure the client.</returns>
    /// <remarks>
    /// This method adds a <see cref="HttpMessageHandler"/> that automatically signs outgoing HTTP requests
    /// using the specified HMAC policy and optional scheme.
    /// </remarks>
    public static IHttpClientBuilder AddHmacHttpMessageHandler(
        this IHttpClientBuilder builder, 
        string policy, 
        string scheme
    ) => builder.AddHttpMessageHandler(provider => 
            GetDelegatingHandler(provider, policy, scheme));

    /// <summary>
    /// Creates a <see cref="DelegatingHandler"/> that uses the specified policy and optional scheme to sign outgoing HTTP requests.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve services.</param>
    /// <param name="policy">The name of the registered policy to be used for signing the requests.</param>
    /// <param name="scheme">The optional scheme to be used for signing the requests.</param>
    /// <returns>A <see cref="DelegatingHandler"/> that will sign outgoing HTTP requests with the specified policy and scheme.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="policy"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="policy"/> and <paramref name="scheme"/> configuration results in a null <see cref="HmacManager"/> instance.</exception>
    internal static DelegatingHandler GetDelegatingHandler(
        IServiceProvider serviceProvider,
        string policy,
        string? scheme = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));

        var hmacManagerFactory = serviceProvider.GetRequiredService<IHmacManagerFactory>();
        var hmacManager = (policy, scheme) switch
        {
            (_, null) => hmacManagerFactory.Create(policy),
            (_, _)    => hmacManagerFactory.Create(policy, scheme)
        };

        ArgumentNullException.ThrowIfNull(hmacManager, nameof(hmacManager));

        // Logging is a convenience here, not a dependency: a container that never called AddLogging
        // must still be able to sign requests.
        var logger = serviceProvider.GetService<ILogger<HmacDelegatingHandler>>()
            ?? NullLogger<HmacDelegatingHandler>.Instance;

        return new HmacDelegatingHandler(hmacManager, logger);
    }
}