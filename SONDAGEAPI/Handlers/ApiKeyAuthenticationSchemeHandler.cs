using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SONDAGEAPI.Data;
using SONDAGEAPI.Schemes;

namespace SONDAGEAPI.Handlers;

// Source - https://stackoverflow.com/a/75059938
// Posted by SergVro
// Retrieved 2026-09-17, License - CC BY-SA 4.0
public class ApiKeyAuthenticationSchemeHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options, 
    ILoggerFactory logger, 
    UrlEncoder encoder,
    ApplicationDbContext db) : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var provided = Context.Request.Headers["X-API-KEY"].ToString();
        if (string.IsNullOrEmpty(provided))
            return AuthenticateResult.Fail("Missing X-API-KEY");

        if (!Guid.TryParse(provided, out var keyGuid))
            return AuthenticateResult.Fail("Invalid X-API-KEY");

        var apiKey = await db.ApiKeys.SingleOrDefaultAsync(k => k.Key == keyGuid);
        if (apiKey is not { IsActive: true })
            return AuthenticateResult.Fail("Invalid or expired X-API-KEY");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
            new Claim(ClaimTypes.Name, $"ApiKey:{apiKey.Id}")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}


