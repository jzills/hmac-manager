using HmacManager.Kubernetes;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Load the chart-generated policy config mounted at a well-known path.
// optional: true so the app starts normally outside Kubernetes.
// reloadOnChange: true lets HmacManager pick up edits (e.g. a rotated key) without
// a pod restart — see HmacPolicyCollectionReloader in the HmacManager library.
// Private keys are never written to this file — they arrive via a separate mounted
// Secret volume, read below via AddKeyPerFile, so ordering is not a concern.
builder.Configuration.AddJsonFile("/etc/hmac-manager/config.json", optional: true, reloadOnChange: true);

// Private keys, one file per policy, named to match the "HmacManager:{i}:Keys:PrivateKey"
// config path (KeyPerFile's default "__" section delimiter, mirroring the JSON structure
// above). Mounted from a projected Secret volume so, like config.json, it can be watched
// and reloaded — see kubernetes/chart/templates/deployment.yaml.
builder.Configuration.AddKeyPerFile("/etc/hmac-manager/secrets", optional: true, reloadOnChange: true);

// Register a shared distributed cache for nonce storage when a Redis connection
// string is provided. This must run BEFORE AddHmacManager: the library calls
// TryAddSingleton<IDistributedCache, MemoryDistributedCache>(), so without a real
// IDistributedCache registered first, CacheType=Distributed silently falls back to
// an in-process cache that is not shared across replicas.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = redisConnectionString);
}

builder.Services.AddHmacManager(builder.Configuration.GetSection("HmacManager"));
builder.Services.AddScoped<ExtAuthzHandler>();
builder.Services.AddScoped<SignHandler>();

// The dev-only signing helper listens on a dedicated port so a request whose
// original path is "/sign" arriving on the main (verify) port can never reach
// SignHandler — which would return 200 and let the waypoint forward an
// unverified request. The sign port is opened only in Development and is not
// exposed by the Kubernetes Service, so the mesh cannot route to it.
var signPort = builder.Configuration.GetValue("SignPort", 8081);
if (builder.Environment.IsDevelopment())
{
    var urls = (builder.Configuration[WebHostDefaults.ServerUrlsKey] ?? "http://+:8080")
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Append($"http://+:{signPort}")
        .ToArray();

    builder.WebHost.UseUrls(urls);
}

var app = builder.Build();

if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    app.Logger.LogWarning(
        "No Redis connection string configured (ConnectionStrings__Redis). The nonce " +
        "cache is in-process and is NOT shared across replicas — run a single replica " +
        "or configure Redis for multi-replica replay protection.");
}

if (app.Environment.IsDevelopment())
{
    // Branch on the connection's local port instead of MapPost so /sign is bound
    // to the sign port only. On the main verify port this predicate is false and
    // the request falls through to the ext-authz verification fallback below.
    app.UseWhen(
        ctx => ctx.Connection.LocalPort == signPort
            && HttpMethods.IsPost(ctx.Request.Method)
            && ctx.Request.Path == "/sign",
        branch => branch.Run(async ctx =>
        {
            var request = await ctx.Request.ReadFromJsonAsync<SignRequest>();
            var handler = ctx.RequestServices.GetRequiredService<SignHandler>();

            IResult result = request is null
                ? Results.BadRequest("Invalid sign request body.")
                : await handler.SignAsync(request);

            await result.ExecuteAsync(ctx);
        }));
}

// Catch-all: Envoy HTTP ext-authz forwards the original request's method and path
// to the ext-authz service, so this must match any method and any path.
app.MapFallback(async (HttpContext ctx, ExtAuthzHandler handler) =>
    await handler.CheckAsync(ctx));

app.Run();
