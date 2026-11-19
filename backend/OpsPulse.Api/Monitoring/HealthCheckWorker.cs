using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Data;

namespace OpsPulse.Api.Monitoring;

public sealed class HealthCheckWorker(IServiceScopeFactory scopes, IHealthProbe probe,
    IOptions<MonitoringOptions> options, TimeProvider clock, ILogger<HealthCheckWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds), clock);
        do
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Health polling cycle failed; retrying next interval."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        // Probe concurrently, then persist one atomic batch on SQLite's single writer.
        var observations = new ConcurrentBag<(string ServiceId, ProbeResult Result, DateTime CheckedAt)>();
        await Parallel.ForEachAsync(options.Value.Services,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (definition, token) =>
            {
                var result = await probe.CheckAsync(definition, token);
                observations.Add((definition.Id, result, clock.GetUtcNow().UtcDateTime));
            });
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPulseDbContext>();
        var services = await db.Services.Where(s => s.IsEnabled).ToDictionaryAsync(s => s.Id, cancellationToken);
        foreach (var observation in observations)
        {
            var service = services[observation.ServiceId];
            service.Status = observation.Result.Status;
            service.ResponseTimeMs = observation.Result.ResponseTimeMs;
            service.LastCheckedAt = observation.CheckedAt;
            db.HealthChecks.Add(new()
            {
                ServiceId = service.Id, Status = observation.Result.Status,
                ResponseTimeMs = observation.Result.ResponseTimeMs, CheckedAt = observation.CheckedAt
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
        await db.HealthChecks.Where(h => h.CheckedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
