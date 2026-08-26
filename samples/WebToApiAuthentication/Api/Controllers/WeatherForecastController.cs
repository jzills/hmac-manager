using System.Security.Claims;
using HmacManager.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// Everything on this controller requires a request signed with "MyPolicy"
// under the "RequireAccountAndEmail" scheme. An unsigned request, one signed
// with a different policy, or one whose X-Account was altered after signing
// never reaches an action — it is rejected with a 401.
[ApiController]
[Route("api/[controller]")]
[HmacAuthenticate(Policy = "MyPolicy", Scheme = "RequireAccountAndEmail")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    [HttpGet("")]
    public IEnumerable<WeatherForecast> Get() =>
        Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            Date: DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC: Random.Shared.Next(-20, 55),
            Summary: Summaries[Random.Shared.Next(Summaries.Length)]))
        .ToArray();

    // The signature covers the body, so a request whose JSON was altered in
    // transit is rejected before this runs.
    [HttpPost("")]
    public IActionResult Post([FromBody] WeatherForecastPost forecast) => Ok(new
    {
        forecast.Summary,

        // These come from the scheme's headers, turned into claims by
        // OnAuthenticationSuccessAsync in Program.cs.
        Account = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Email = User.FindFirstValue(ClaimTypes.Email)
    });
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherForecastPost(string? Summary);
