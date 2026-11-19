using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Contracts;
using OpsPulse.Api.Data;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Services;

public sealed class StatusService(OpsPulseDbContext db, TimeProvider clock, IOptions<MonitoringOptions> options)
{
    public async Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddHours(-24);
        var services = await db.Services.AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Name).ToListAsync(cancellationToken);
        var records = await db.HealthChecks.AsNoTracking().Where(h => h.Service.IsEnabled && h.CheckedAt >= cutoff && h.CheckedAt <= now)
            .ToListAsync(cancellationToken);
        var history = records.ToLookup(h => h.ServiceId);
        var firstBucket = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-23);
        return services.Select(service =>
        {
            var samples = history[service.Id].ToArray();
            var buckets = Enumerable.Range(0, 24).Select(index =>
            {
                var from = firstBucket.AddHours(index);
                var bucketSamples = samples.Where(h => h.CheckedAt >= from && h.CheckedAt < from.AddHours(1)).ToArray();
                return new HistoryBucket(from, bucketSamples.Length == 0 ? null : bucketSamples.Max(h => h.Status),
                    bucketSamples.Length, Availability(bucketSamples));
            }).ToArray();
            var stale = service.LastCheckedAt is null || service.LastCheckedAt < now.AddSeconds(-options.Value.IntervalSeconds * 3);
            return new ServiceDto(service.Id, service.Name, service.Description,
                stale && service.Status == ServiceStatus.Healthy ? ServiceStatus.Degraded : service.Status,
                service.LastCheckedAt, service.ResponseTimeMs, stale, Availability(samples), samples.Length, buckets);
        }).ToArray();
    }

    public async Task<StatusSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var services = await GetServicesAsync(cancellationToken);
        var incidents = await db.Incidents.AsNoTracking().Include(i => i.Service).OrderByDescending(i => i.CreatedAt)
            .Take(20).ToListAsync(cancellationToken);
        var openCount = await db.Incidents.CountAsync(i => i.Status == IncidentStatus.Open, cancellationToken);
        return new(clock.GetUtcNow().UtcDateTime, options.Value.Services.Any(s => s.Kind == ProbeKind.Demo),
            services.Max(s => s.Status), options.Value.IntervalSeconds, openCount, services, incidents.Select(ToDto).ToArray());
    }

    public static IncidentDto ToDto(Incident incident) => new(incident.Id, incident.ServiceId, incident.Service.Name,
        incident.Title, incident.Description, incident.Severity, incident.Status, incident.CreatedAt, incident.ResolvedAt, incident.Resolution);

    private static double? Availability(HealthCheckRecord[] samples) => samples.Length == 0 ? null :
        Math.Round(samples.Count(h => h.Status == ServiceStatus.Healthy) * 100d / samples.Length, 2);
}
