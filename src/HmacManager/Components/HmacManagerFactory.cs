using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Common.Extensions;
using HmacManager.Diagnostics;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;
using HmacManager.Schemes;

namespace HmacManager.Components;

/// <summary>
/// A factory class for creating instances of <see cref="IHmacManager"/> using specified policies and configurations.
/// </summary>
public class HmacManagerFactory : IHmacManagerFactory
{
    /// <summary>
    /// Manages the set of HMAC policies applied during request validation.
    /// </summary>
    protected readonly IHmacPolicyCollection Policies;

    /// <summary>
    /// Tracks nonces to prevent replay attacks.
    /// </summary>
    protected readonly IComponentCollection<INonceCache> Caches;

    /// <summary>
    /// Factory for creating instances of <see cref="IHmacHeaderParser"/> to parse HMAC headers from requests.
    /// </summary>
    protected readonly IHmacHeaderParserFactory HeaderParserFactory;

    /// <summary>
    /// Factory for creating instances of <see cref="IHmacHeaderBuilder"/> to construct HMAC headers for requests.
    /// </summary>
    protected readonly IHmacHeaderBuilderFactory HeaderBuilderFactory;

    /// <summary>
    /// The <see cref="ILoggerFactory"/> used to build the logger handed to every
    /// <see cref="HmacManager"/> this factory creates.
    /// </summary>
    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    /// The <see cref="ILogger"/> policy resolution failures are recorded to.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacManagerFactory"/> class that does not log.
    /// </summary>
    /// <param name="policies">The collection of HMAC policies.</param>
    /// <param name="caches">The cache for storing nonces to prevent replay attacks.</param>
    /// <param name="headerParserFactory">Factory to create HMAC header parsers.</param>
    /// <param name="headerBuilderFactory">Factory to create HMAC header builders.</param>
    public HmacManagerFactory(
        IHmacPolicyCollection policies,
        IComponentCollection<INonceCache> caches,
        IHmacHeaderParserFactory headerParserFactory,
        IHmacHeaderBuilderFactory headerBuilderFactory
    ) : this(policies, caches, headerParserFactory, headerBuilderFactory, NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacManagerFactory"/> class with specified dependencies.
    /// </summary>
    /// <param name="policies">The collection of HMAC policies.</param>
    /// <param name="caches">The cache for storing nonces to prevent replay attacks.</param>
    /// <param name="headerParserFactory">Factory to create HMAC header parsers.</param>
    /// <param name="headerBuilderFactory">Factory to create HMAC header builders.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used by this factory and by every manager it creates.</param>
    /// <remarks>
    /// Resolved through dependency injection in preference to the logger-less overload, so an
    /// application that registers HmacManager the normal way gets logging without asking for it.
    /// </remarks>
    public HmacManagerFactory(
        IHmacPolicyCollection policies,
        IComponentCollection<INonceCache> caches,
        IHmacHeaderParserFactory headerParserFactory,
        IHmacHeaderBuilderFactory headerBuilderFactory,
        ILoggerFactory loggerFactory
    )
    {
        Policies = policies;
        Caches = caches;
        HeaderParserFactory = headerParserFactory;
        HeaderBuilderFactory = headerBuilderFactory;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<HmacManagerFactory>();
    }

    /// <inheritdoc/>
    public IHmacManager? Create(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));
        
        if (TryGetPolicyCache(policy, out var options, out var cache))
        {
            return CreateManager(options, cache);
        }
        else
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public IHmacManager? Create(string policy, string? scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));
        
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return Create(policy);
        }

        if (!TryGetPolicyCache(policy, out var options, out var cache))
        {
            return null;
        }

        // A scheme that was asked for and does not resolve is a mistake, not a request to sign
        // without one. The lookup used to happen inside CreateManager with its result unchecked, so
        // an unresolved name became a null Scheme on the options — which HmacManager treats as "no
        // scheme". The manager signed successfully, omitting the scheme header and every header
        // value the scheme names from the signing content, and the verifier rejected it as a
        // signature mismatch. Refusing here is the same answer an unknown policy name already gets.
        if (options.Schemes.Get(scheme) is null)
        {
            HmacLog.SchemeNotFound(Logger, policy, scheme);
            return null;
        }

        return CreateManager(options, cache, scheme);
    }

    /// <summary>
    /// Creates an instance of <see cref="HmacManager"/> based on the specified options and cache.
    /// </summary>
    /// <param name="options">The HMAC policy options.</param>
    /// <param name="cache">The nonce cache.</param>
    /// <returns>A new <see cref="HmacManager"/> instance.</returns>
    private HmacManager CreateManager(HmacPolicy options, INonceCache cache) =>
        new HmacManager(
            CreateOptions(options.Name!, options.Nonce.MaxAgeInSeconds),
            CreateFactory(options.Keys, options.Algorithms, options.SigningContentBuilder),
            CreateResultFactory(options.Name!),
            cache,
            LoggerFactory.CreateLogger<HmacManager>()
        );

    /// <summary>
    /// Creates an instance of <see cref="HmacManager"/> based on the specified options, cache, and scheme.
    /// </summary>
    /// <param name="options">The HMAC policy options.</param>
    /// <param name="cache">The nonce cache.</param>
    /// <param name="scheme">The scheme to use for HMAC management.</param>
    /// <returns>A new <see cref="HmacManager"/> instance.</returns>
    private HmacManager CreateManager(HmacPolicy options, INonceCache cache, string scheme) =>
        new HmacManager(
            CreateOptions(
                options.Name!, 
                options.Nonce.MaxAgeInSeconds, 
                options.Schemes.Get(scheme)
            ),
            CreateFactory(options.Keys, options.Algorithms, options.SigningContentBuilder),
            CreateResultFactory(options.Name!, scheme),
            cache,
            LoggerFactory.CreateLogger<HmacManager>()
        );

    /// <summary>
    /// Creates an <see cref="HmacFactory"/> for signing requests based on specified keys, algorithms, and content builder.
    /// </summary>
    private HmacFactory CreateFactory(
        KeyCredentials keys, 
        Algorithms algorithms,
        SigningContentBuilder signingContentBuilder
    ) => new HmacFactory(CreateProvider(keys, algorithms, signingContentBuilder));

    /// <summary>
    /// Creates an <see cref="HmacResultFactory"/> for producing HMAC results with the given policy and optional scheme.
    /// </summary>
    /// <param name="policy">The HMAC policy name.</param>
    /// <param name="scheme">An optional scheme associated with the policy.</param>
    /// <returns>An <see cref="HmacResultFactory"/> instance.</returns>
    private HmacResultFactory CreateResultFactory(
        string policy, 
        string? scheme = null
    ) => new HmacResultFactory(policy, scheme);

    /// <summary>
    /// Creates an <see cref="HmacSignatureProvider"/> using the specified keys, algorithms, and signing content builder.
    /// </summary>
    private HmacSignatureProvider CreateProvider(KeyCredentials keys, Algorithms algorithms, SigningContentBuilder signingContentBuilder) =>
        new HmacSignatureProvider(new HmacSignatureProviderOptions 
        { 
            Keys = keys, 
            Algorithms = algorithms,
            ContentHashGenerator = CreateContentHashGenerator(algorithms.ContentHashAlgorithm),
            SignatureHashGenerator = CreateSignatureHashGenerator(keys.PrivateKey, algorithms.SigningHashAlgorithm),
            SigningContentBuilder = signingContentBuilder
        });

    /// <summary>
    /// Creates <see cref="HmacManagerOptions"/> with specified policy, expiration time, and optional scheme.
    /// </summary>
    private HmacManagerOptions CreateOptions(
        string policy, 
        int maxAgeInSeconds, 
        Scheme? scheme = null
    ) => 
        new HmacManagerOptions(policy) 
        {
            MaxAgeInSeconds = maxAgeInSeconds, 
            Scheme = scheme,
            HeaderBuilder = HeaderBuilderFactory.Create(),
            HeaderParser = HeaderParserFactory.Create()
        };

    /// <summary>
    /// Creates a <see cref="ContentHashGenerator"/> based on the specified content hash algorithm.
    /// </summary>
    /// <param name="contentHashAlgorithm">The content hash algorithm to use.</param>
    /// <returns>A <see cref="ContentHashGenerator"/> instance.</returns>
    private ContentHashGenerator CreateContentHashGenerator(
        ContentHashAlgorithm contentHashAlgorithm
    ) => new ContentHashGenerator(contentHashAlgorithm);

    /// <summary>
    /// Creates a <see cref="SignatureHashGenerator"/> using the specified private key and signing hash algorithm.
    /// </summary>
    /// <param name="privateKey">The private key for signing.</param>
    /// <param name="signingHashAlgorithm">The signing hash algorithm to use.</param>
    /// <returns>A <see cref="SignatureHashGenerator"/> instance.</returns>
    private SignatureHashGenerator CreateSignatureHashGenerator(
        string privateKey, 
        SigningHashAlgorithm signingHashAlgorithm
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey, nameof(privateKey));
        return new SignatureHashGenerator(privateKey, signingHashAlgorithm);
    }

    /// <summary>
    /// Attempts to retrieve a policy and its associated nonce cache based on the given policy name.
    /// </summary>
    /// <param name="policy">The name of the policy.</param>
    /// <param name="options">The output policy options if the policy is found.</param>
    /// <param name="cache">The output nonce cache if found.</param>
    /// <returns><c>true</c> if the policy and cache are found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// Both lookups fail the same way from the caller's perspective — a null manager — so each one
    /// says which it was. A missing nonce cache in particular is a registration mistake that is
    /// otherwise indistinguishable from a typo in a policy name.
    /// </remarks>
    private bool TryGetPolicyCache(string policy, out HmacPolicy options, out INonceCache cache)
    {
        cache = default!;

        if (!Policies.TryGetValue(policy, out options))
        {
            HmacLog.PolicyNotFound(Logger, policy);
            return false;
        }

        if (!Caches.TryGetValue(Enum.GetName(options.Nonce.CacheType)!, out cache))
        {
            HmacLog.NonceCacheNotFound(Logger, policy, options.Nonce.CacheType);
            return false;
        }

        return true;
    }
}