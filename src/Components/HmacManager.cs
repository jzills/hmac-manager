using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HmacManager.Caching;
using HmacManager.Caching.Extensions;
using HmacManager.Diagnostics;
using HmacManager.Extensions;

namespace HmacManager.Components;

/// <summary>
/// A class representing a <see cref="HmacManager"/>.
/// </summary>
public class HmacManager : IHmacManager
{
    /// <inheritdoc/>
    public HmacManagerOptions Options { get; }

    /// <summary>
    /// An implementation of <see cref="IHmacFactory"/> for creating <see cref="Hmac"/> objects. 
    /// </summary>
    protected readonly IHmacFactory Factory;

    /// <summary>
    /// An implementation of <see cref="IHmacResultFactory"/> for creating <see cref="HmacResult"/> objects. 
    /// </summary>
    protected readonly IHmacResultFactory ResultFactory;

    /// <summary>
    /// An implementation of <see cref="INonceCache"/> for storing nonce values.
    /// </summary>
    protected readonly INonceCache Cache;

    /// <summary>
    /// The <see cref="ILogger"/> signing and verification outcomes are recorded to.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Creates a <see cref="HmacManager"/> object that does not log.
    /// </summary>
    /// <param name="options"><see cref="HmacManagerOptions"/></param>
    /// <param name="factory"><see cref="IHmacFactory"/></param>
    /// <param name="resultFactory"><see cref="IHmacResultFactory"/></param>
    /// <param name="cache"><see cref="INonceCache"/></param>
    /// <returns>A <see cref="HmacManager"/> object.</returns>
    public HmacManager(
        HmacManagerOptions options,
        IHmacFactory factory,
        IHmacResultFactory resultFactory,
        INonceCache cache
    ) : this(options, factory, resultFactory, cache, NullLogger<HmacManager>.Instance)
    {
    }

    /// <summary>
    /// Creates a <see cref="HmacManager"/> object that records signing and verification outcomes
    /// to <paramref name="logger"/>.
    /// </summary>
    /// <param name="options"><see cref="HmacManagerOptions"/></param>
    /// <param name="factory"><see cref="IHmacFactory"/></param>
    /// <param name="resultFactory"><see cref="IHmacResultFactory"/></param>
    /// <param name="cache"><see cref="INonceCache"/></param>
    /// <param name="logger">The <see cref="ILogger"/> to record outcomes to.</param>
    /// <returns>A <see cref="HmacManager"/> object.</returns>
    public HmacManager(
        HmacManagerOptions options,
        IHmacFactory factory,
        IHmacResultFactory resultFactory,
        INonceCache cache,
        ILogger<HmacManager> logger
    )
    {
        Options = options;
        Factory = factory;
        ResultFactory = resultFactory;
        Cache = cache;
        Logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each way a request can fail verification is rejected — and logged — separately. A single
    /// "verification failed" would be true but useless: an expired signature, a replayed nonce and
    /// a genuine signature mismatch have entirely different causes and entirely different fixes.
    /// </remarks>
    public async Task<HmacResult> VerifyAsync(HttpRequestMessage request)
    {
        if (!TryParseHmac(request.Headers, out var incomingHmac) || incomingHmac is null)
        {
            HmacLog.VerificationHeadersMissing(
                Logger, request.Method, request.RequestUri, Options.Policy, Options.Scheme?.Name);

            return ResultFactory.Failure();
        }

        if (!incomingHmac.DateRequested.HasValidDateRequested(Options.MaxAgeInSeconds))
        {
            HmacLog.VerificationRequestExpired(
                Logger, Options.Policy, incomingHmac.DateRequested, Options.MaxAgeInSeconds);

            return ResultFactory.Failure();
        }

        if (!await Cache.IsValidNonceAsync(incomingHmac.Nonce, incomingHmac.DateRequested))
        {
            HmacLog.VerificationNonceReplayed(Logger, Options.Policy, incomingHmac.Nonce);

            return ResultFactory.Failure();
        }

        var hmacVerification = await Factory.CreateAsync(request, incomingHmac);
        if (!hmacVerification.IsVerified(incomingHmac))
        {
            HmacLog.VerificationSignatureMismatch(Logger, Options.Policy, Options.Scheme?.Name);
            HmacLog.VerificationSignatureMismatchDetail(
                Logger,
                Options.Policy,
                hmacVerification.Signature,
                hmacVerification.SigningContent,
                incomingHmac.Signature);

            return ResultFactory.Failure();
        }

        HmacLog.RequestVerified(
            Logger, request.Method, request.RequestUri, Options.Policy, Options.Scheme?.Name);

        return ResultFactory.Success(hmacVerification);
    }

    /// <inheritdoc/>
    public async Task<HmacResult> SignAsync(HttpRequestMessage request)
    {
        var hmac = await Factory.CreateAsync(request, Options.Policy, Options.Scheme);
        if (hmac is null)
        {
            HmacLog.SigningFailed(
                Logger, request.Method, request.RequestUri, Options.Policy, Options.Scheme?.Name);

            return ResultFactory.Failure();
        }

        var headers = Options.HeaderBuilder.CreateBuilder(Options, hmac).Build();
        request.Headers.AddRange(headers);

        HmacLog.RequestSigned(
            Logger, request.Method, request.RequestUri, Options.Policy, Options.Scheme?.Name, hmac.Nonce);
        HmacLog.SigningContentComputed(Logger, Options.Policy, hmac.SigningContent);

        return ResultFactory.Success(hmac);
    }

    /// <summary>
    /// Attempts to parse the provided HTTP request headers into an <see cref="Hmac"/> object.
    /// </summary>
    /// <param name="headers">The <see cref="HttpRequestHeaders"/> to parse.</param>
    /// <param name="value">
    /// When this method returns, contains the parsed <see cref="Hmac"/> object if parsing was successful;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if parsing was successful and a valid <see cref="Hmac"/> object was created; otherwise, <c>false</c>.
    /// </returns>
    private bool TryParseHmac(HttpRequestHeaders headers, out Hmac? value)
    {
        var hmacPartial = Options.HeaderParser.CreateParser(headers).Parse(out var signature);
        if (Options.Scheme is null)
        {
            value = new Hmac
            {
                Policy = hmacPartial.Policy,
                Scheme = hmacPartial.Scheme,
                Signature = signature ?? string.Empty,
                DateRequested = hmacPartial.DateRequested,
                Nonce = hmacPartial.Nonce,
                HeaderValues = []
            };
        }
        else if (headers.TryParseHeaders(Options.Scheme, out var headerValues))
        {
            value = new Hmac
            {
                Policy = hmacPartial.Policy,
                Scheme = hmacPartial.Scheme,
                Signature = signature ?? string.Empty,
                DateRequested = hmacPartial.DateRequested,
                Nonce = hmacPartial.Nonce,
                HeaderValues = headerValues.ToArray()
            };
        }
        else
        {
            value = null;
        }

        return value is not null;
    }
}
