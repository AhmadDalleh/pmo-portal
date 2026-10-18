using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pmo.Application.Common.Abstractions;

namespace Pmo.Api.Controllers;

[ApiController]
[AllowAnonymous]   // uptime monitors and Docker health checks have no token
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IDatabaseHealthCheck _databaseHealthCheck;

    public HealthController(IDatabaseHealthCheck databaseHealthCheck)
    {
        _databaseHealthCheck = databaseHealthCheck;
    }

    /// <summary>200 when PostgreSQL answers, 503 when it does not.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var databaseUp = await _databaseHealthCheck.CanConnectAsync(cancellationToken);

        return databaseUp
            ? Ok(new HealthResponse("healthy", "up"))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("unhealthy", "down"));
    }
}
