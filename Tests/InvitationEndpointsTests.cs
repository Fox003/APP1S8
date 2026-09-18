using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using SONDAGEAPI.Security.Participants;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Émission des invitations. C'est le seul endroit où le jeton en clair existe :
/// ces tests vérifient sa qualité (entropie, format), le fait qu'il ne soit jamais
/// persisté en clair, et les bornes de validité.
/// </summary>
public class InvitationEndpointsTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    [Fact]
    public async Task Une_invitation_valide_est_creee_avec_son_jeton_en_clair()
    {
        var client = factory.CreateApiKeyClient();
        var survey = await ApiScenario.CreateSurveyAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{survey.Id}/invitations", new CreateInvitationRequest(30));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invitation = await response.Content.ReadFromJsonAsync<CreateInvitationResponse>();
        Assert.NotEqual(Guid.Empty, invitation!.InvitationId);
        Assert.False(string.IsNullOrWhiteSpace(invitation.Token));
        Assert.Equal(factory.Clock.GetUtcNow().AddDays(30), invitation.ExpiresAt);
        Assert.Equal($"/api/sondages/{survey.Id}/invitations/{invitation.InvitationId}",
            response.Headers.Location?.ToString());
    }

    /// <summary>256 bits encodés en base64url : 43 caractères, sans remplissage.</summary>
    [Fact]
    public async Task Le_jeton_porte_256_bits_dalea()
    {
        var client = factory.CreateApiKeyClient();
        var (_, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        Assert.Equal(43, invitation.Token.Length);
        Assert.DoesNotContain("=", invitation.Token);
        Assert.Equal(32, WebEncoders.Base64UrlDecode(invitation.Token).Length);
    }

    [Fact]
    public async Task Deux_invitations_ne_partagent_jamais_le_meme_jeton()
    {
        var client = factory.CreateApiKeyClient();
        var survey = await ApiScenario.CreateSurveyAsync(client);

        var tokens = new HashSet<string>();
        for (var i = 0; i < 25; i++)
        {
            tokens.Add((await ApiScenario.CreateInvitationAsync(client, survey.Id)).Token);
        }

        Assert.Equal(25, tokens.Count);
    }

    /// <summary>
    /// Une fuite de app.db ne doit permettre d'usurper personne : seule l'empreinte
    /// SHA-256 est persistée, le jeton en clair n'existe nulle part en base.
    /// </summary>
    [Fact]
    public async Task Seule_lempreinte_du_jeton_est_persistee()
    {
        var client = factory.CreateApiKeyClient();
        var (_, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var stored = await factory.QueryDatabaseAsync(db =>
            db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.InvitationId));

        Assert.Equal(InvitationToken.HashBytes, stored.TokenHash.Length);
        Assert.Equal(InvitationToken.ComputeHash(invitation.Token), stored.TokenHash);
        Assert.Null(stored.RedeemedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    [InlineData(int.MaxValue)]
    public async Task Une_validite_hors_bornes_est_refusee(int validityDays)
    {
        var client = factory.CreateApiKeyClient();
        var survey = await ApiScenario.CreateSurveyAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{survey.Id}/invitations", new CreateInvitationRequest(validityDays));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("validityDays", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public async Task Une_validite_sur_les_bornes_est_acceptee(int validityDays)
    {
        var client = factory.CreateApiKeyClient();
        var survey = await ApiScenario.CreateSurveyAsync(client);

        var invitation = await ApiScenario.CreateInvitationAsync(client, survey.Id, validityDays);

        Assert.Equal(factory.Clock.GetUtcNow().AddDays(validityDays), invitation.ExpiresAt);
    }

    /// <summary>
    /// L'existence du sondage est vérifiée après la validation d'entrée : une charge
    /// utile invalide ne doit pas servir à sonder quels identifiants existent.
    /// </summary>
    [Fact]
    public async Task Un_sondage_inconnu_ne_produit_aucune_invitation()
    {
        var unknownId = Guid.NewGuid();

        var response = await factory.CreateApiKeyClient().PostAsJsonAsync(
            $"/api/sondages/{unknownId}/invitations", new CreateInvitationRequest(30));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.QueryDatabaseAsync(db =>
            db.Invitations.CountAsync(i => i.SurveyId == unknownId)));
    }

    [Fact]
    public async Task Lemission_dune_invitation_exige_la_cle_dApi()
    {
        var survey = await ApiScenario.CreateSurveyAsync(factory.CreateApiKeyClient());

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/sondages/{survey.Id}/invitations", new CreateInvitationRequest(30));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
