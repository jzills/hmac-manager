using Api;
using HmacManager.Mvc.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Whatever the policies are resolved from, registered normally.
builder.Services.AddScoped<PolicyStore>();

builder.Services
    .AddMemoryCache()
    .AddAuthentication()
    .AddHmac(options =>
    {
        // The whole difference from the baseline sample. Instead of policies
        // fixed at startup, the accessor runs per request with a scoped
        // IServiceProvider — so a key rotated in the store takes effect on the
        // next request, with no restart and no cache to invalidate.
        options.EnableScopedPolicies(serviceProvider =>
            serviceProvider.GetRequiredService<PolicyStore>().GetPolicies());
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
