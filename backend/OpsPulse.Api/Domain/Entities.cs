namespace OpsPulse.Api.Domain;

public enum ServiceStatus { Healthy, Degraded, Down }
public enum IncidentSeverity { Minor, Major, Critical }
public enum IncidentStatus { Open, Resolved }

public sealed class MonitoredService
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public ServiceStatus Status { get; set; } = ServiceStatus.Down;
    public DateTime? LastCheckedAt { get; set; }
    public int? ResponseTimeMs { get; set; }
}

public sealed class HealthCheckRecord
{
    public long Id { get; set; }
    public string ServiceId { get; set; } = "";
    public MonitoredService Service { get; set; } = null!;
    public ServiceStatus Status { get; set; }
    public DateTime CheckedAt { get; set; }
    public int? ResponseTimeMs { get; set; }
}

public sealed class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ServiceId { get; set; } = "";
    public MonitoredService Service { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}
