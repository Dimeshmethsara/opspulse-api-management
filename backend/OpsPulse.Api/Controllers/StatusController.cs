using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPulse.Api.Contracts;
using OpsPulse.Api.Data;
using OpsPulse.Api.Services;

namespace OpsPulse.Api.Controllers;

[ApiController]
public sealed class StatusController(StatusService status, OpsPulseDbContext db) : ControllerBase
{
    /// <summary>API readiness; returns 503 if SQLite cannot be reached.</summary>
    [HttpGet("/health")]
    [ProducesResponseType<HealthResponse>(200)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return Problem(statusCode: 503, title: "Database is unavailable.");
        return Ok(new HealthResponse("Healthy"));
    }

    /// <summary>Public summary: service health, 24-hour sample history and the 20 most recent incidents.</summary>
    [HttpGet("/api/status")]
    public Task<StatusSummary> Summary(CancellationToken cancellationToken) => status.GetSummaryAsync(cancellationToken);

    /// <summary>Public services registered in server configuration. Probe destinations are never exposed.</summary>
    [HttpGet("/api/services")]
    public Task<IReadOnlyList<ServiceDto>> Services(CancellationToken cancellationToken) => status.GetServicesAsync(cancellationToken);
}

public sealed record HealthResponse(string Status);
