using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// The whole difference from the baseline sample: the policy comes from
// configuration rather than a builder delegate. The section name is arbitrary —
// what matters is that it binds to an array of policies.
builder.Services
    .AddMemoryCache()
    .AddAuthentication()
    .AddHmac(builder.Configuration.GetSection("HmacPolicies"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
