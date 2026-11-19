using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Configuration;

public enum ProbeKind { Demo, Http }

public sealed class ServiceDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProbeKind Kind { get; set; } = ProbeKind.Demo;
    public string? Endpoint { get; set; }
    public ServiceStatus DemoStatus { get; set; } = ServiceStatus.Healthy;
    public int DemoLatencyMs { get; set; } = 100;
}

public sealed class MonitoringOptions
{
    public const string Section = "Monitoring";
    public bool WorkerEnabled { get; set; } = true;
    public bool SeedDemoData { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 5;
    public int DegradedAfterMs { get; set; } = 1000;
    public int RetentionDays { get; set; } = 7;
    public string[] AllowedHosts { get; set; } = [];
    public List<ServiceDefinition> Services { get; set; } = [];
}

public sealed class MonitoringOptionsValidator : IValidateOptions<MonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, MonitoringOptions options)
    {
        var errors = new List<string>();
        if (options.IntervalSeconds is < 15 or > 3600) errors.Add("IntervalSeconds must be 15..3600.");
        if (options.TimeoutSeconds is < 1 or > 15) errors.Add("TimeoutSeconds must be 1..15.");
        if (options.DegradedAfterMs < 1 || options.DegradedAfterMs >= options.TimeoutSeconds * 1000)
            errors.Add("DegradedAfterMs must be positive and less than the timeout.");
        if (options.RetentionDays is < 1 or > 90) errors.Add("RetentionDays must be 1..90.");
        if (options.Services.Count is < 1 or > 100) errors.Add("Configure between 1 and 100 services.");
        if (options.Services.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Services.Count)
            errors.Add("Service IDs must be unique (case insensitive).");
        foreach (var service in options.Services)
        {
            if (!Regex.IsMatch(service.Id, "^[a-z][a-z0-9-]{1,49}$")) errors.Add("Service IDs must be lowercase slugs, 2..50 characters.");
            if (string.IsNullOrWhiteSpace(service.Name) || service.Name.Length > 100 || service.Description.Length > 500)
                errors.Add("Each service requires a name up to 100 characters and a description up to 500.");
            if (!Enum.IsDefined(service.Kind) || !Enum.IsDefined(service.DemoStatus) || service.DemoLatencyMs is < 0 or > 15000)
                errors.Add("Invalid probe type, demo status or demo latency.");
            if (service.Kind == ProbeKind.Http && !IsAllowedEndpoint(service.Endpoint, options.AllowedHosts))
                errors.Add("HTTP probes require an allowlisted DNS host, HTTPS port 443, and no credentials, query or fragment.");
            if (service.Kind == ProbeKind.Demo && service.Endpoint is not null)
                errors.Add("Demo probes cannot have an endpoint.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public static bool IsAllowedEndpoint(string? endpoint, string[] allowedHosts) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
        uri.HostNameType == UriHostNameType.Dns && !uri.IsLoopback &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        allowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
}
