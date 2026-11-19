using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsPulse.Api.Data;

namespace OpsPulse.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestKey = "test-only-key-for-integration-tests-123456789";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public bool WritesEnabled { get; init; } = true;

    public ApiFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OpsPulse"] = "Data Source=:memory:",
            ["Monitoring:WorkerEnabled"] = "false",
            ["IncidentApi:WriteKey"] = WritesEnabled ? TestKey : null
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<OpsPulseDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OpsPulseDbContext>>();
            services.AddDbContext<OpsPulseDbContext>(options => options.UseSqlite(connection));
        });
    }

    public HttpClient CreateOperatorClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-OpsPulse-Key", TestKey);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}
