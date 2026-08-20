using System.Security.Claims;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Identical to the baseline sample. The Api has no idea its caller is not .NET,
// which is the point — the wire format is the contract, not the language.
builder.Services
    .AddMemoryCache()
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));

            // The same base64 string the TypeScript client is configured with.
            policy.UsePrivateKey("dGhpc0lzTXlTdXBlckNvb2xQcml2YXRlS2V5");
            policy.UseMemoryCache(30);
            policy.AddScheme("RequireAccountAndEmail", scheme =>
            {
                scheme.AddHeader("X-Account");
                scheme.AddHeader("X-Email");
            });
        });

        options.Events = new HmacEvents
        {
            OnAuthenticationSuccessAsync = (context, hmacResult) => Task.FromResult(new Claim[]
            {
                new(ClaimTypes.NameIdentifier, context.Request.Headers["X-Account"].ToString()),
                new(ClaimTypes.Email, context.Request.Headers["X-Email"].ToString())
            })
        };
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
