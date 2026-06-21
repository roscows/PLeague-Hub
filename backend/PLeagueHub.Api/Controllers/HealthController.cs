using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public HealthController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse
        {
            Environment = _environment.EnvironmentName,
            CheckedAtUtc = DateTime.UtcNow
        });
    }
}
