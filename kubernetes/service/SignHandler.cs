using System.Text;
using HmacManager.Components;
using HmacManager.Kubernetes.Diagnostics;

namespace HmacManager.Kubernetes;

internal record SignRequest(
    string Policy,
    string Method,
    string Uri,
    string? Scheme = null,
    string? Body = null,
    // Extra request headers to attach before signing. Required to exercise a Scheme: the library
    // reads a scheme's header values off the request being signed (HmacBuilder.TryParseHeaders), so
    // e.g. {"X-UserId":"user-123"} must be present on the message for the scheme to fold it into the
    // signature. Ignored for schemeless requests.
    Dictionary<string, string>? Headers = null
);

internal class SignHandler(IHmacManagerFactory factory, ILogger<SignHandler> logger)
{
    public async Task<IResult> SignAsync(SignRequest signRequest)
    {
        // Policy, Method and Uri are the irreducible inputs: without them there is nothing to sign.
        // Body is deliberately optional — a signed GET (or any bodyless request) omits it — as are
        // Scheme and Headers. Reject a malformed envelope with a 400 here rather than letting a null
        // required field surface as an unhandled 500 deeper in (e.g. factory.Create throwing on a
        // null policy).
        if (string.IsNullOrWhiteSpace(signRequest.Policy) ||
            string.IsNullOrWhiteSpace(signRequest.Method) ||
            string.IsNullOrWhiteSpace(signRequest.Uri))
        {
            ServiceLog.SignRequestInvalid(logger);
            return Results.BadRequest(
                "A sign request requires 'Policy', 'Method' and 'Uri'. 'Body' is optional — omit it to sign a request with no body.");
        }

        var manager = factory.Create(signRequest.Policy, signRequest.Scheme);
        if (manager is null)
        {
            ServiceLog.SignPolicyNotFound(logger, signRequest.Policy);
            return Results.NotFound($"Policy '{signRequest.Policy}' not found.");
        }

        var httpRequest = new HttpRequestMessage(new HttpMethod(signRequest.Method), signRequest.Uri);

        if (signRequest.Body is not null)
            httpRequest.Content = new StringContent(signRequest.Body, Encoding.UTF8, "application/json");

        // Attach any caller-supplied headers before signing so a scheme's headers are present on the
        // request for the library to include in the signature.
        if (signRequest.Headers is not null)
            foreach (var (name, value) in signRequest.Headers)
                httpRequest.Headers.TryAddWithoutValidation(name, value);

        var result = await manager.SignAsync(httpRequest);
        if (!result.IsSuccess)
        {
            ServiceLog.SignFailed(logger, signRequest.Policy);
            return Results.Problem("Signing failed.");
        }

        ServiceLog.SignRequestCompleted(logger, signRequest.Method, signRequest.Policy, signRequest.Scheme);

        var headers = httpRequest.Headers
            .Concat(httpRequest.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .Where(h => h.Key.StartsWith("Hmac", StringComparison.OrdinalIgnoreCase) || h.Key == "Authorization")
            .ToDictionary(h => h.Key, h => h.Value.FirstOrDefault() ?? string.Empty);

        return Results.Ok(headers);
    }
}
