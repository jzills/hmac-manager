using System.Text;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Two HMAC policies — two different callers, each with their own key pair.
builder.Services
    .AddMemoryCache()
    .AddAuthentication()
    .AddHmac(options =>
    {
        options.AddPolicy("MyPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("eb8e9dae-08bd-4883-80fe-1d9a103b30b5"));
            policy.UsePrivateKey(Convert.ToBase64String(Encoding.UTF8.GetBytes("thisIsMySuperCoolPrivateKey")));
            policy.UseMemoryCache(30);
            policy.AddScheme("RequireAccountAndEmail", scheme =>
            {
                scheme.AddHeader("X-Account");
                scheme.AddHeader("X-Email");
            });
        });

        options.AddPolicy("AdminPolicy", policy =>
        {
            policy.UsePublicKey(Guid.Parse("ac2f1dae-08bd-4883-80fe-1d9a103b30b5"));
            policy.UsePrivateKey(Convert.ToBase64String(Encoding.UTF8.GetBytes("thisIsMySuperCoolAdminPrivateKey")));
            policy.UseMemoryCache(30);
            policy.AddScheme("RequireAccountAndEmail", scheme =>
            {
                scheme.AddHeader("X-Account");
                scheme.AddHeader("X-Email");
            });
        });
    });

// Authenticating says the request was signed by someone holding a key.
// Authorizing says which of them an endpoint is for.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireMyPolicy", policy =>
        policy.RequireHmacAuthentication("MyPolicy", "RequireAccountAndEmail"));

    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireHmacAuthentication("AdminPolicy", "RequireAccountAndEmail"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
