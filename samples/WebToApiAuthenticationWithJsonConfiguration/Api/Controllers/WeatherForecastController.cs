using System.Security.Claims;
using HmacManager.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

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

    [HttpPost("")]
    public IActionResult Post([FromBody] WeatherForecastPost forecast) => Ok(new
    {
        forecast.Summary,

        // No HmacEvents in this sample. These claims come from the ClaimType
        // declared against each header in appsettings.json.
        Account = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Email = User.FindFirstValue(ClaimTypes.Email)
    });
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherForecastPost(string? Summary);
