using Microsoft.EntityFrameworkCore;
using OpsPulse.Api.Contracts;
using OpsPulse.Api.Data;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Services;

public sealed class IncidentService(OpsPulseDbContext db, TimeProvider clock)
{
    public async Task<IncidentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await db.Incidents.AsNoTracking().Include(i => i.Service).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return incident is null ? null : StatusService.ToDto(incident);
    }

    public async Task<IReadOnlyList<IncidentDto>> ListAsync(int limit, CancellationToken cancellationToken) =>
        (await db.Incidents.AsNoTracking().Include(i => i.Service).OrderByDescending(i => i.CreatedAt)
            .Take(limit).ToListAsync(cancellationToken)).Select(StatusService.ToDto).ToArray();

    public async Task<IncidentDto?> CreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var service = await db.Services.SingleOrDefaultAsync(s => s.Id == request.ServiceId && s.IsEnabled, cancellationToken);
        if (service is null) return null;
        var incident = new Incident
        {
            Service = service, ServiceId = service.Id, Title = request.Title.Trim(), Description = request.Description.Trim(),
            Severity = request.Severity, CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);
        return StatusService.ToDto(incident);
    }

    public async Task<IncidentDto?> ResolveAsync(Guid id, string resolution, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // Conditional update prevents concurrent resolutions from overwriting the first resolution.
        await db.Incidents.Where(i => i.Id == id && i.Status == IncidentStatus.Open)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(i => i.Status, IncidentStatus.Resolved)
                .SetProperty(i => i.ResolvedAt, now)
                .SetProperty(i => i.Resolution, resolution.Trim()), cancellationToken);
        var incident = await db.Incidents.AsNoTracking().Include(i => i.Service).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return incident is null ? null : StatusService.ToDto(incident);
    }
}
