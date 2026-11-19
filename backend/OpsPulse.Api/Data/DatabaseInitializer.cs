using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Data;

public sealed class DatabaseInitializer(OpsPulseDbContext db, IOptions<MonitoringOptions> options, TimeProvider clock)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        var config = options.Value;
        var stored = await db.Services.ToListAsync(cancellationToken);
        foreach (var existing in stored) existing.IsEnabled = config.Services.Any(s => s.Id == existing.Id);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var definition in config.Services)
        {
            var service = stored.SingleOrDefault(s => s.Id == definition.Id);
            var isNew = service is null;
            if (service is null)
            {
                service = new MonitoredService { Id = definition.Id };
                db.Services.Add(service);
            }
            service.Name = definition.Name;
            service.Description = definition.Description;
            service.IsEnabled = true;
            if (!isNew || !config.SeedDemoData || definition.Kind != ProbeKind.Demo) continue;
            for (var index = 95; index >= 0; index--)
            {
                var status = index < 5 ? definition.DemoStatus : ServiceStatus.Healthy;
                if (definition.Id == "payments" && index is >= 34 and <= 36) status = ServiceStatus.Degraded;
                db.HealthChecks.Add(new HealthCheckRecord
                {
                    ServiceId = definition.Id, CheckedAt = now.AddMinutes(-index * 15), Status = status,
                    ResponseTimeMs = status == ServiceStatus.Down ? null : definition.DemoLatencyMs + index % 7 * 9
                });
            }
            service.Status = definition.DemoStatus;
            service.ResponseTimeMs = definition.DemoStatus == ServiceStatus.Down ? null : definition.DemoLatencyMs;
            service.LastCheckedAt = now;
            if (definition.DemoStatus != ServiceStatus.Healthy)
                db.Incidents.Add(new Incident
                {
                    ServiceId = definition.Id, Title = definition.DemoStatus == ServiceStatus.Down ? "Inventory synchronization unavailable" : "Notification delivery delays",
                    Description = definition.DemoStatus == ServiceStatus.Down
                        ? "The inventory synchronization pipeline is unavailable. Orders are accepted; stock updates will resume after recovery."
                        : "Some email and push notifications are delayed. The team is investigating queue processing latency.",
                    Severity = definition.DemoStatus == ServiceStatus.Down ? IncidentSeverity.Major : IncidentSeverity.Minor,
                    CreatedAt = now.AddMinutes(-65)
                });
            if (definition.Id == "payments")
                db.Incidents.Add(new Incident
                {
                    ServiceId = definition.Id, Title = "Elevated payment processing latency",
                    Description = "A provider slowdown increased payment confirmation times. No transactions were lost.",
                    Severity = IncidentSeverity.Minor, Status = IncidentStatus.Resolved,
                    CreatedAt = now.AddHours(-9), ResolvedAt = now.AddHours(-8),
                    Resolution = "Provider latency recovered. The queue drained and transaction timings returned to normal."
                });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
