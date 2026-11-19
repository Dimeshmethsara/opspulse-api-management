using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPulse.Api.Contracts;
using OpsPulse.Api.Services;

namespace OpsPulse.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController(IncidentService incidents) : ControllerBase
{
    /// <summary>Most recent public incidents, including resolved incidents.</summary>
    [HttpGet]
    public Task<IReadOnlyList<IncidentDto>> List([FromQuery, Range(1, 100)] int limit = 50,
        CancellationToken cancellationToken = default) => incidents.ListAsync(limit, cancellationToken);

    /// <summary>Read an individual public incident.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<IncidentDto>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var incident = await incidents.GetAsync(id, cancellationToken);
        return incident is null ? Problem(statusCode: 404, title: "Incident was not found.") : Ok(incident);
    }

    /// <summary>Create a public incident for a predefined service. Requires X-OpsPulse-Key.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<IncidentDto>(201)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Create(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        if (request.Title.Trim().Length < 3 || request.Description.Trim().Length < 10)
            return Problem(statusCode: 400, title: "Incident title and description must contain meaningful text.");
        var incident = await incidents.CreateAsync(request, cancellationToken);
        return incident is null
            ? Problem(statusCode: 400, title: "The service is not registered.")
            : CreatedAtAction(nameof(Get), new { id = incident.Id }, incident);
    }

    /// <summary>Resolve an incident. Repeated requests preserve the first resolution. Requires X-OpsPulse-Key.</summary>
    [HttpPatch("{id}/resolve")]
    [Authorize]
    [ProducesResponseType<IncidentDto>(200)]
    [ProducesResponseType<ValidationProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Resolve(Guid id, ResolveIncidentRequest request, CancellationToken cancellationToken)
    {
        if (request.Resolution.Trim().Length < 10)
            return Problem(statusCode: 400, title: "Resolution must contain at least 10 meaningful characters.");
        var incident = await incidents.ResolveAsync(id, request.Resolution, cancellationToken);
        return incident is null ? Problem(statusCode: 404, title: "Incident was not found.") : Ok(incident);
    }
}
