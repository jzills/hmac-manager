using System.Security.Claims;
using System.Text;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    // HmacManager does not register cache services itself, and UseMemoryCache
    // below resolves IMemoryCache from the container.
    .AddMemoryCache()
    .AddAuthentication()
    .AddHmac(options =>
    {
        // The same policy as the Web project. Both ends must agree on the
        // keys, the algorithms and the scheme's headers, or every request
        // is rejected as a signature mismatch.
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));
            policy.UsePrivateKey(Convert.ToBase64String(Encoding.UTF8.GetBytes("thisIsMySuperCoolPrivateKey")));

            // How long a signed request stays valid, and how long its nonce is
            // remembered so the same request cannot be replayed inside that window.
            policy.UseMemoryCache(30);

            // A scheme folds these header values into the signature, so they
            // cannot be altered in transit.
            policy.AddScheme("RequireAccountAndEmail", scheme =>
            {
                scheme.AddHeader("X-Account");
                scheme.AddHeader("X-Email");
            });
        });

        options.Events = new HmacEvents
        {
            // Return false to reject a public key — this is where a real
            // application would look the key up rather than trusting the policy.
            OnValidateKeysAsync = (context, keys) => Task.FromResult(true),

            // The claims the authenticated principal carries. The scheme's
            // header values are available on context.Request.Headers.
            OnAuthenticationSuccessAsync = (context, hmacResult) => Task.FromResult(new Claim[]
            {
                new(ClaimTypes.NameIdentifier, context.Request.Headers["X-Account"].ToString()),
                new(ClaimTypes.Email, context.Request.Headers["X-Email"].ToString())
            }),

            OnAuthenticationFailureAsync = (context, hmacResult) =>
                Task.FromResult(new Exception("An error occurred authenticating request."))
        };
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
