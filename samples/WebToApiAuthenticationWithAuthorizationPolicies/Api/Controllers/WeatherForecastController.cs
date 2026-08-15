using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

// No [HmacAuthenticate] here. The requirement is expressed as an ordinary
// ASP.NET Core authorization policy, so it composes with everything else
// authorization can already do.
[ApiController]
[Route("api/[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    [HttpGet("")]
    [Authorize(Policy = "RequireMyPolicy")]
    public IEnumerable<WeatherForecast> Get() =>
        Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            Date: DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC: Random.Shared.Next(-20, 55),
            Summary: Summaries[Random.Shared.Next(Summaries.Length)]))
        .ToArray();

    // Signed with MyPolicy this returns 403, not 401: the caller proved who
    // they are, they are just not who this endpoint is for.
    [HttpDelete("")]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult Delete() => Ok(new { Deleted = true });
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
