using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SONDAGEAPI.Data;

namespace SONDAGEAPI.Security.Participants;

// Contrairement au livrable 1 (middleware), on passe ici par l'abstraction
// d'authentification d'ASP.NET Core : on obtient [Authorize], HttpContext.User,
// l'adhésion par endpoint plutôt que l'exemption, et la composition avec d'autres
// schémas (le JWT de F-O par exemple).
internal sealed class ParticipantTokenAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ParticipantTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db,
        TimeProvider timeProvider)
        : base(options, logger, encoder)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Count != 1 -> en-tête absent ou dupliqué
        if (!Request.Headers.TryGetValue(ParticipantTokenDefaults.HeaderName, out var values)
            || values.Count != 1)
        {
            return AuthenticateResult.NoResult();
        }

        var presented = values[0];
        if (string.IsNullOrWhiteSpace(presented))
        {
            return AuthenticateResult.NoResult();
        }

        var presentedHash = InvitationToken.ComputeHash(presented);

        // Recherche PAR empreinte, sur l'index unique : aucun secret n'est comparé
        // en mémoire, donc aucune fuite par canal temporel côté application.
        var invitation = await _db.Invitations
            .AsNoTracking()
            .Where(i => i.TokenHash == presentedHash)
            .Select(i => new { i.Id, i.SurveyId, i.ExpiresAt })
            .SingleOrDefaultAsync();

        if (invitation is null)
        {
            Logger.LogWarning("Jeton de participation inconnu. {Method} {Path}",
                Request.Method, Request.Path);
            return AuthenticateResult.Fail("Jeton invalide.");
        }

        // Comparaison en mémoire plutôt qu'en SQL : SQLite stocke DateTimeOffset en TEXT
        // et la traduction des comparaisons est fragile. Une seule ligne, donc gratuit.
        if (invitation.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            Logger.LogWarning("Jeton de participation expiré. Invitation {InvitationId}", invitation.Id);
            return AuthenticateResult.Fail("Jeton invalide.");
        }

        // On ne consomme PAS le jeton ici. L'authentification identifie, elle ne modifie
        // pas l'état : sinon une simple lecture brûlerait la participation. La rédemption
        // appartient au seul endpoint de soumission.
        var identity = new ClaimsIdentity(
        [
            new Claim(ParticipantClaims.InvitationId, invitation.Id.ToString()),
            new Claim(ParticipantClaims.SurveyId, invitation.SurveyId.ToString())
        ], Scheme.Name);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    // Réponse identique que le jeton soit absent, inconnu, malformé ou expiré :
    // distinguer les cas dirait à un attaquant quels jetons existent.
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Non autorisé",
                detail: "Jeton de participation absent ou invalide.")
            .ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Interdit",
                detail: "Ce jeton ne donne pas accès à cette ressource.")
            .ExecuteAsync(Context);
    }
}
