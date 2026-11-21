using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Planning.Server.Tests.DayPlanning;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string Scheme = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var value) ||
            string.IsNullOrWhiteSpace(value.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, value.ToString())
        };
        if (Request.Headers.TryGetValue(RoleHeader, out var role) &&
            !string.IsNullOrWhiteSpace(role.ToString()))
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var identity = new ClaimsIdentity(claims, Scheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity), Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
