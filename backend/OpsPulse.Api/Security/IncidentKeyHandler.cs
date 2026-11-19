using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace OpsPulse.Api.Security;

public sealed class IncidentKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IncidentKey";
    public const string HeaderName = "X-OpsPulse-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = configuration["IncidentApi:WriteKey"];
        var provided = Request.Headers[HeaderName];
        if (string.IsNullOrWhiteSpace(expected) || provided.Count != 1 || provided[0] is not { Length: > 0 and <= 1024 } supplied)
            return Task.FromResult(AuthenticateResult.NoResult());
        var valid = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        if (!valid) return Task.FromResult(AuthenticateResult.Fail("Invalid incident write key."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "incident-operator")], SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var unavailable = string.IsNullOrWhiteSpace(configuration["IncidentApi:WriteKey"]);
        Response.StatusCode = unavailable ? 503 : 401;
        Response.Headers.WWWAuthenticate = SchemeName;
        var problems = Context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails
            {
                Status = Response.StatusCode,
                Title = unavailable ? "Incident writes are disabled." : "An incident write key is required.",
                Detail = unavailable ? "An operator must configure the server's incident write key." : "Supply a valid X-OpsPulse-Key header."
            }
        });
    }
}
