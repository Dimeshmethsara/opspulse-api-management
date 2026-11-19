using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Data;
using OpsPulse.Api.Monitoring;
using OpsPulse.Api.Security;
using OpsPulse.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
});
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var problem = new ValidationProblemDetails(context.ModelState)
    {
        Status = 400, Title = "Request validation failed.", Instance = context.HttpContext.Request.Path
    };
    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddOptions<MonitoringOptions>().BindConfiguration(MonitoringOptions.Section).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<MonitoringOptions>, MonitoringOptionsValidator>();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("OpsPulse") ?? "Data Source=storage/opspulse.db";
var sqlite = new SqliteConnectionStringBuilder(connectionString);
if (sqlite.DataSource != ":memory:" && !string.IsNullOrEmpty(sqlite.DataSource))
{
    sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
}
builder.Services.AddDbContext<OpsPulseDbContext>(options => options.UseSqlite(sqlite.ToString()));
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<StatusService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddSingleton<IHealthProbe, SafeHttpProbe>();
builder.Services.AddHttpClient("SafeProbe", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(5), MaxResponseHeadersLength = 16,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1), ConnectCallback = PublicNetwork.ConnectAsync
    });
builder.Services.AddHostedService<HealthCheckWorker>();

var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:4200";
if (!Uri.TryCreate(frontendOrigin, UriKind.Absolute, out var origin) ||
    origin.Scheme is not ("http" or "https") || origin.GetLeftPart(UriPartial.Authority) != frontendOrigin)
    throw new InvalidOperationException("Cors:FrontendOrigin must be one HTTP(S) origin without a trailing slash or path.");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(frontendOrigin)
    .WithMethods("GET").WithHeaders("Content-Type")));
var writeKey = builder.Configuration["IncidentApi:WriteKey"];
if (writeKey is not null && (string.IsNullOrWhiteSpace(writeKey) || writeKey.Length is < 32 or > 1024))
    throw new InvalidOperationException("IncidentApi:WriteKey must have between 32 and 1024 characters when supplied.");
builder.Services.AddAuthentication(IncidentKeyHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, IncidentKeyHandler>(IncidentKeyHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "OpsPulse API", Version = "v1",
            Description = "Public service health and incident history. Incident mutations require a server-managed X-OpsPulse-Key. Probe configuration is private."
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [IncidentKeyHandler.SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = IncidentKeyHandler.HeaderName
            }
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any())
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(IncidentKeyHandler.SchemeName, context.Document)] = []
            }];
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
        .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext });
});
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
app.Run();

public partial class Program;
