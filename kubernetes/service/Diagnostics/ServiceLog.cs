namespace HmacManager.Kubernetes.Diagnostics;

/// <summary>
/// The catalogue of log messages emitted by the ext-authz service.
/// </summary>
/// <remarks>
///     <para>
///         Envoy sees one bit from this service: allow or deny. Everything a denied request could
///         tell an operator — which policy, which check failed, whether the request even claimed to
///         be signed — exists only here. These messages are the entire diagnostic surface of a 403,
///         so each distinct reason gets its own event.
///     </para>
///     <para>
///         The library logs the underlying verification failure against its own categories; these
///         messages add the mesh-level context (method and original path) the library never sees.
///     </para>
///     <para>
///         Event ids: 3000–3099 authorization checks, 3100–3199 the development signing helper.
///     </para>
/// </remarks>
internal static partial class ServiceLog
{
    // ---------------------------------------------------------------------------------------------
    // Authorization checks — 3000–3099
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Records an allowed request.
    /// </summary>
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Debug,
        Message = "Allowing {Method} {Path}: verified against policy \"{Policy}\" and scheme \"{Scheme}\".")]
    public static partial void RequestAllowed(
        ILogger logger, string method, string path, string policy, string? scheme);

    /// <summary>
    /// Records a request that never claimed to be signed. Debug rather than Warning: unauthenticated
    /// traffic reaching a protected route is routine, and a Warning per request would be noise.
    /// </summary>
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Denying {Method} {Path}: the request carries no usable HMAC authorization header.")]
    public static partial void RequestUnauthenticated(ILogger logger, string method, string path);

    /// <summary>
    /// Records a request that was signed but did not verify.
    /// </summary>
    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Denying {Method} {Path}: HMAC verification failed.")]
    public static partial void RequestVerificationFailed(ILogger logger, string method, string path);

    /// <summary>
    /// Records a request rejected because its HMAC headers were unusable — an unknown policy, a
    /// missing header, or a malformed one.
    /// </summary>
    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Denying {Method} {Path}: the request's HMAC headers could not be interpreted.")]
    public static partial void RequestHeadersRejected(
        ILogger logger, string method, string path, Exception exception);

    // ---------------------------------------------------------------------------------------------
    // Development signing helper — 3100–3199
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Records a signed request produced by the development-only signing endpoint.
    /// </summary>
    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Debug,
        Message = "Signed a {Method} request for policy \"{Policy}\" and scheme \"{Scheme}\".")]
    public static partial void SignRequestCompleted(
        ILogger logger, string method, string policy, string? scheme);

    /// <summary>
    /// Records a signing request naming a policy this host does not have.
    /// </summary>
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Warning,
        Message = "Cannot sign: no policy named \"{Policy}\" is configured.")]
    public static partial void SignPolicyNotFound(ILogger logger, string policy);

    /// <summary>
    /// Records a signing request that resolved a policy but still produced no signature.
    /// </summary>
    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Cannot sign: policy \"{Policy}\" resolved but produced no signature.")]
    public static partial void SignFailed(ILogger logger, string policy);
}
