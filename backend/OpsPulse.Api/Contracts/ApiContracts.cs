using System.ComponentModel.DataAnnotations;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Contracts;

public sealed record HistoryBucket(DateTime From, ServiceStatus? Status, int Samples, double? AvailabilityPercent);
public sealed record ServiceDto(string Id, string Name, string Description, ServiceStatus Status,
    DateTime? LastCheckedAt, int? ResponseTimeMs, bool IsStale, double? AvailabilityPercent,
    int SampleCount, IReadOnlyList<HistoryBucket> History);
public sealed record IncidentDto(Guid Id, string ServiceId, string ServiceName, string Title, string Description,
    IncidentSeverity Severity, IncidentStatus Status, DateTime CreatedAt, DateTime? ResolvedAt, string? Resolution);
public sealed record StatusSummary(DateTime GeneratedAt, bool IsDemo, ServiceStatus OverallStatus,
    int PollIntervalSeconds, int OpenIncidentCount, IReadOnlyList<ServiceDto> Services, IReadOnlyList<IncidentDto> RecentIncidents);

public sealed class CreateIncidentRequest
{
    [Required, RegularExpression("^[a-z][a-z0-9-]{1,49}$")]
    public required string ServiceId { get; init; }
    [Required, StringLength(120, MinimumLength = 3)]
    public required string Title { get; init; }
    [Required, StringLength(1000, MinimumLength = 10)]
    public required string Description { get; init; }
    [Required, EnumDataType(typeof(IncidentSeverity))]
    public required IncidentSeverity Severity { get; init; }
}

public sealed class ResolveIncidentRequest
{
    [Required, StringLength(1000, MinimumLength = 10)]
    public required string Resolution { get; init; }
}
