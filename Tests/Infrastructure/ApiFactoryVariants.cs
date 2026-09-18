using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SONDAGEAPI.Security.Participants;

namespace Tests.Infrastructure;

/// <summary>
/// Même API, mais hors Development : <c>UseSondageOpenApi</c> ne doit alors câbler
/// ni /openapi ni Swagger. C'est la seconde branche du <c>if</c> d'environnement, et
/// c'est aussi une exigence de sécurité — ne pas publier le schéma en production.
/// </summary>
public sealed class ProductionApiFactory : SondageApiFactory
{
    public ProductionApiFactory() => EnvironmentName = Environments.Production;
}

/// <summary>
/// Déclare un port HTTPS pour que <c>UseHttpsRedirection</c> ait quelque chose vers
/// quoi rediriger. Sans cette clé de configuration le middleware se désactive
/// silencieusement et la redirection du livrable 1 ne serait jamais démontrée.
/// </summary>
public sealed class HttpsRedirectionApiFactory : SondageApiFactory
{
    public const int HttpsPort = 7016;

    protected override void RegisterTestSettings(IDictionary<string, string?> settings) =>
        settings["HTTPS_PORT"] = HttpsPort.ToString();
}

/// <summary>
/// Remplace le gestionnaire d'authentification par un gestionnaire qui émet des
/// revendications volontairement malformées.
/// <para>
/// Objectif : prouver que les endpoints ne font pas confiance aveuglément au jeton
/// validé en amont. La politique d'autorisation n'exige que la *présence* de la
/// revendication <c>invitation_id</c> ; elle ne dit rien de son format. Un
/// gestionnaire compromis, ou un second schéma d'authentification ajouté plus tard
/// (le JWT par exemple), pourrait donc laisser passer une revendication non
/// convertible en GUID. Les endpoints re-valident, et ces tests le démontrent.
/// </para>
/// </summary>
public sealed class ForgedClaimsApiFactory : SondageApiFactory
{
    /// <summary>Jeton factice : produit une revendication d'invitation illisible.</summary>
    public const string InvitationIdNotAGuid = "forge-invitation-illisible";

    /// <summary>Jeton factice : invitation valide, mais aucune revendication de sondage.</summary>
    public const string SurveyIdMissing = "forge-sondage-absent";

    protected override void RegisterTestServices(IServiceCollection services)
    {
        services.AddTransient<ForgedClaimsAuthenticationHandler>();

        // AddScheme place le même objet AuthenticationSchemeBuilder dans SchemeMap et
        // dans la liste lue par AuthenticationSchemeProvider : muter son HandlerType
        // suffit à substituer le gestionnaire, sans réenregistrer le schéma (ce que
        // AddScheme refuserait).
        services.PostConfigure<AuthenticationOptions>(options =>
            options.SchemeMap[ParticipantTokenDefaults.AuthenticationScheme].HandlerType =
                typeof(ForgedClaimsAuthenticationHandler));
    }
}

/// <summary>Gestionnaire de test qui fabrique les revendications demandées par l'en-tête.</summary>
public sealed class ForgedClaimsAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ParticipantTokenDefaults.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims = values[0] switch
        {
            ForgedClaimsApiFactory.InvitationIdNotAGuid =>
            [
                new Claim(ParticipantClaims.InvitationId, "ceci-n-est-pas-un-guid"),
                new Claim(ParticipantClaims.SurveyId, Guid.NewGuid().ToString())
            ],
            ForgedClaimsApiFactory.SurveyIdMissing =>
            [
                new Claim(ParticipantClaims.InvitationId, Guid.NewGuid().ToString())
            ],
            _ => []
        };

        if (claims.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
