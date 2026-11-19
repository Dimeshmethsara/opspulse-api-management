using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpsPulse.Api.Contracts;
using OpsPulse.Api.Data;
using OpsPulse.Api.Domain;
using OpsPulse.Api.Monitoring;

namespace OpsPulse.Tests;

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    public ApiIntegrationTests(ApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/status")]
    [InlineData("/api/services")]
    [InlineData("/api/incidents")]
    public async Task Public_endpoints_work_without_credentials(string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Summary_has_real_sqlite_history_and_no_private_configuration()
    {
        var response = await client.GetAsync("/api/status");
        var body = await response.Content.ReadAsStringAsync();
        var summary = JsonSerializer.Deserialize<StatusSummary>(body, Json)!;
        Assert.Equal(6, summary.Services.Count);
        Assert.True(summary.IsDemo);
        Assert.Equal(ServiceStatus.Down, summary.OverallStatus);
        Assert.All(summary.Services, service =>
        {
            Assert.Equal(24, service.History.Count);
            Assert.True(service.SampleCount >= 95);
            Assert.InRange(service.AvailabilityPercent!.Value, 0, 100);
            Assert.Equal(DateTimeKind.Utc, service.LastCheckedAt!.Value.Kind);
        });
        Assert.Contains(summary.RecentIncidents, incident => incident.Status == IncidentStatus.Resolved);
        foreach (var forbidden in new[] { "endpoint", "allowedHosts", "writeKey", "connectionString", ApiFactory.TestKey })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Operator_can_create_resolve_and_repeat_resolution_without_overwriting()
    {
        using var operatorClient = factory.CreateOperatorClient();
        var created = await operatorClient.PostAsJsonAsync("/api/incidents", new
        {
            serviceId = "payments", title = "Payment provider timeout", description = "Some payment confirmations are delayed.", severity = "Major"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        var incident = await created.Content.ReadFromJsonAsync<IncidentDto>(Json);
        Assert.Equal(IncidentStatus.Open, incident!.Status);
        var resolved = await operatorClient.PatchAsJsonAsync($"/api/incidents/{incident.Id}/resolve", new { resolution = "The provider recovered and the queue drained." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var first = await resolved.Content.ReadFromJsonAsync<IncidentDto>(Json);
        var repeat = await operatorClient.PatchAsJsonAsync($"/api/incidents/{incident.Id}/resolve", new { resolution = "This text should never replace the first resolution." });
        var second = await repeat.Content.ReadFromJsonAsync<IncidentDto>(Json);
        Assert.Equal(IncidentStatus.Resolved, second!.Status);
        Assert.NotNull(second.ResolvedAt);
        Assert.Equal(first!.ResolvedAt, second.ResolvedAt);
        Assert.Equal(first.Resolution, second.Resolution);
        var all = await client.GetFromJsonAsync<IncidentDto[]>("/api/incidents", Json);
        Assert.Contains(all!, item => item.Id == incident.Id && item.Status == IncidentStatus.Resolved);
    }

    [Theory]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"  \",\"description\":\"Valid description text\",\"severity\":\"Major\"}")]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"Valid title\",\"description\":\"short\",\"severity\":\"Major\"}")]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"Valid title\",\"description\":\"Valid description text\",\"severity\":99}")]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"Valid title\",\"description\":\"Valid description text\",\"severity\":\"Unknown\"}")]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"Valid title\",\"description\":\"Valid description text\"}")]
    [InlineData("{\"serviceId\":\"missing\",\"title\":\"Valid title\",\"description\":\"Valid description text\",\"severity\":\"Minor\"}")]
    [InlineData("{\"serviceId\":\"payments\",\"title\":\"Valid title\",\"description\":\"Valid description text\",\"severity\":\"Minor\",\"endpoint\":\"http://169.254.169.254\"}")]
    [InlineData("{broken")]
    public async Task Invalid_incidents_return_consistent_problem_details(string body)
    {
        using var operatorClient = factory.CreateOperatorClient();
        var response = await operatorClient.PostAsync("/api/incidents", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        await AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/incidents?limit=0", 400)]
    [InlineData("/api/incidents?limit=101", 400)]
    [InlineData("/api/incidents?limit=nope", 400)]
    [InlineData("/missing", 404)]
    public async Task Invalid_routes_and_query_inputs_return_problem_details(string path, int expected)
        => await AssertProblem(await client.GetAsync(path), (HttpStatusCode)expected);

    [Fact]
    public async Task Invalid_and_missing_incident_ids_and_resolution_are_handled()
    {
        using var operatorClient = factory.CreateOperatorClient();
        var body = new { resolution = "Recovery confirmed by the operations team." };
        await AssertProblem(await operatorClient.PatchAsJsonAsync("/api/incidents/not-a-guid/resolve", body), HttpStatusCode.BadRequest);
        await AssertProblem(await operatorClient.PatchAsJsonAsync($"/api/incidents/{Guid.NewGuid()}/resolve", body), HttpStatusCode.NotFound);
        await AssertProblem(await operatorClient.PatchAsJsonAsync($"/api/incidents/{Guid.NewGuid()}/resolve", new { resolution = "   " }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Incident_writes_require_a_valid_server_key()
    {
        var request = new { serviceId = "payments", title = "Provider slow", description = "An upstream provider is responding slowly.", severity = "Minor" };
        await AssertProblem(await client.PostAsJsonAsync("/api/incidents", request), HttpStatusCode.Unauthorized);
        using var invalid = factory.CreateClient();
        invalid.DefaultRequestHeaders.Add("X-OpsPulse-Key", "invalid-key");
        await AssertProblem(await invalid.PostAsJsonAsync("/api/incidents", request), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unconfigured_write_key_disables_mutations()
    {
        using var disabled = new ApiFactory { WritesEnabled = false };
        using var noKeyClient = disabled.CreateClient();
        await AssertProblem(await noKeyClient.PostAsJsonAsync("/api/incidents", new { }), HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.OK, (await noKeyClient.GetAsync("/api/status")).StatusCode);
    }

    [Fact]
    public async Task Cors_allows_only_configured_frontend_origin_and_read_methods()
    {
        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        allowed.Headers.Add("Origin", "http://localhost:4200");
        var response = await client.SendAsync(allowed);
        Assert.Equal("http://localhost:4200", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        using var blocked = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        blocked.Headers.Add("Origin", "https://untrusted.example");
        Assert.False((await client.SendAsync(blocked)).Headers.Contains("Access-Control-Allow-Origin"));
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/incidents");
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        var preflightResponse = await client.SendAsync(preflight);
        Assert.DoesNotContain("POST", string.Join(",", preflightResponse.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task Openapi_has_all_routes_and_write_security_but_no_private_configuration()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.StartsWith("3.0.", root.GetProperty("openapi").GetString());
        var paths = root.GetProperty("paths");
        Assert.Equal(6, paths.EnumerateObject().Count());
        var writeSecurity = paths.GetProperty("/api/incidents").GetProperty("post").GetProperty("security");
        Assert.True(writeSecurity[0].TryGetProperty("IncidentKey", out _));
        Assert.False(paths.GetProperty("/api/status").GetProperty("get").TryGetProperty("security", out _));
        var createSchema = root.GetProperty("components").GetProperty("schemas").GetProperty("CreateIncidentRequest");
        Assert.Equal(120, createSchema.GetProperty("properties").GetProperty("title").GetProperty("maxLength").GetInt32());
        Assert.Contains(createSchema.GetProperty("required").EnumerateArray(), field => field.GetString() == "severity");
        Assert.DoesNotContain(ApiFactory.TestKey, body);
        Assert.DoesNotContain("Data Source", body);
    }

    [Fact]
    public async Task Polling_persists_current_state_and_history_and_applies_retention()
    {
        using var isolated = new ApiFactory();
        _ = isolated.CreateClient();
        using var scope = isolated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpsPulseDbContext>();
        db.HealthChecks.Add(new() { ServiceId = "identity", Status = ServiceStatus.Down, CheckedAt = DateTime.UtcNow.AddDays(-8) });
        await db.SaveChangesAsync();
        var before = await db.HealthChecks.CountAsync();
        var worker = isolated.Services.GetServices<IHostedService>().OfType<HealthCheckWorker>().Single();
        await worker.PollOnceAsync(CancellationToken.None);
        Assert.Equal(before + 5, await db.HealthChecks.CountAsync());
        Assert.False(await db.HealthChecks.AnyAsync(h => h.CheckedAt < DateTime.UtcNow.AddDays(-7)));
        Assert.Equal(ServiceStatus.Down, (await db.Services.AsNoTracking().SingleAsync(s => s.Id == "inventory")).Status);
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.True(body.RootElement.TryGetProperty("title", out _));
    }
}
