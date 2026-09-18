using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using SONDAGEAPI.Models;
using SONDAGEAPI.Security.Participants;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Livrable 4 — authentification du participant.
/// Vecteurs d'attaque couverts : soumission sans jeton, jeton inventé, jeton d'un
/// autre sondage (déplacement latéral), jeton consommé rejoué, charge utile
/// démesurée, et fuite d'information par les messages d'erreur.
/// </summary>
public class ParticipationEndpointsTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    [Fact]
    public async Task Une_soumission_valide_est_acceptee_et_enregistree()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var participant = factory.CreateParticipantClient(invitation.Token);

        var response = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Très satisfait"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await factory.QueryDatabaseAsync(db =>
            db.Responses.AsNoTracking().SingleAsync(r => r.SurveyId == surveyId));
        Assert.Equal("Très satisfait", stored.Content);
        Assert.Equal(factory.Clock.GetUtcNow(), stored.SubmittedAt);
    }

    /// <summary>
    /// Le corps de la réponse 201 est volontairement vide : renvoyer l'identifiant de
    /// la réponse fournirait une poignée pour la recorréler au participant.
    /// </summary>
    [Fact]
    public async Task La_reponse_201_ne_divulgue_aucun_identifiant_de_reponse()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/sondages/{surveyId}/reponses", response.Headers.Location?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrWhiteSpace(body) || body == "null");
    }

    /// <summary>LE point du livrable : un jeton ne sert qu'une fois.</summary>
    [Fact]
    public async Task Un_jeton_deja_utilise_est_refuse_en_409()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var participant = factory.CreateParticipantClient(invitation.Token);

        var first = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Première réponse"));
        var second = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Seconde réponse"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // La seconde soumission ne doit avoir laissé aucune trace.
        Assert.Equal(1, await factory.QueryDatabaseAsync(db =>
            db.Responses.CountAsync(r => r.SurveyId == surveyId)));
    }

    /// <summary>
    /// La rédemption est datée au jour près, pas à la seconde : un horodatage précis
    /// permettrait de rapprocher une invitation d'un SurveyResponse.SubmittedAt et de
    /// désanonymiser le participant.
    /// </summary>
    [Fact]
    public async Task La_redemption_nest_datee_quau_jour_pres()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        var stored = await factory.QueryDatabaseAsync(db =>
            db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.InvitationId));

        Assert.Equal(DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime), stored.RedeemedAt);
    }

    [Fact]
    public async Task Sans_jeton_de_participation_la_soumission_est_refusee_en_401()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, _) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Un_jeton_vide_ou_blanc_est_refuse_en_401(string token)
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, _) = await ApiScenario.CreateInvitedParticipantAsync(client);
        client.DefaultRequestHeaders.TryAddWithoutValidation(ParticipantTokenDefaults.HeaderName, token);

        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Même motif que pour la clé d'API : un en-tête dupliqué est rejeté.</summary>
    [Fact]
    public async Task Un_jeton_duplique_est_refuse_meme_si_une_valeur_est_bonne()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            ParticipantTokenDefaults.HeaderName, new[] { invitation.Token, "valeur-injectee" });

        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Un_jeton_inconnu_est_refuse_en_401()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, _) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await factory.CreateParticipantClient("jeton-completement-invente")
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Pas d'oracle d'existence : un jeton inconnu et un jeton absent doivent donner
    /// exactement la même réponse, sinon l'API confirme quels jetons existent.
    /// </summary>
    [Fact]
    public async Task Les_refus_sont_indiscernables_entre_jeton_absent_et_jeton_inconnu()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, _) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var withoutToken = await client.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));
        var withUnknownToken = await factory.CreateParticipantClient("jeton-inconnu").PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(withoutToken.StatusCode, withUnknownToken.StatusCode);
        Assert.Equal(await withoutToken.Content.ReadAsStringAsync(),
            await withUnknownToken.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Déplacement latéral : une invitation au sondage A ne doit pas ouvrir le
    /// sondage B, même si le porteur est parfaitement authentifié.
    /// </summary>
    [Fact]
    public async Task Un_jeton_ne_donne_acces_quau_sondage_qui_la_emis()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyA, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var surveyB = await ApiScenario.CreateSurveyAsync(client, "Sondage voisin");

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyB.Id}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(surveyA, surveyB.Id);

        // Le refus ne doit pas avoir consommé la participation légitime.
        var invitationRow = await factory.QueryDatabaseAsync(db =>
            db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.InvitationId));
        Assert.Null(invitationRow.RedeemedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Une_reponse_vide_est_refusee(string content)
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest(content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("content", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Une_reponse_plus_longue_que_la_limite_est_refusee()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var oversized = new string('a', SurveyResponse.ContentMaxLength + 1);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest(oversized));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Une_reponse_exactement_a_la_limite_est_acceptee()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var atLimit = new string('a', SurveyResponse.ContentMaxLength);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest(atLimit));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// La validation d'entrée passe avant l'examen du jeton : une charge utile
    /// invalide doit être rejetée sans consommer la participation.
    /// </summary>
    [Fact]
    public async Task Une_reponse_invalide_ne_consomme_pas_la_participation()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var participant = factory.CreateParticipantClient(invitation.Token);

        var refused = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest(" "));
        var accepted = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse valide"));

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Une_reponse_portant_une_injection_SQL_est_stockee_litteralement()
    {
        const string payload = "'); DROP TABLE Responses;--";
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest(payload));

        var stored = await factory.QueryDatabaseAsync(db =>
            db.Responses.AsNoTracking().SingleAsync(r => r.SurveyId == surveyId));
        Assert.Equal(payload, stored.Content);
    }

    /// <summary>
    /// Garde défensive : une invitation dont le sondage a disparu. L'état est
    /// fabriqué avec les clés étrangères désactivées, parce que la cascade de SQLite
    /// l'interdit — mais un jour de migration bâclée, lui, ne l'interdit pas.
    /// </summary>
    [Fact]
    public async Task Une_invitation_orpheline_retourne_404_plutot_quune_erreur_serveur()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var deleted = await factory.ExecuteWithoutForeignKeysAsync(
            "DELETE FROM Surveys WHERE Id = $id;", ("$id", surveyId));
        Assert.Equal(1, deleted);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Consultation du statut de participation
    // -----------------------------------------------------------------------

    /// <summary>
    /// S'authentifier ne consomme pas le jeton : on peut interroger son statut
    /// autant de fois qu'on veut avant de répondre.
    /// </summary>
    [Fact]
    public async Task Le_statut_passe_de_false_a_true_et_la_lecture_ne_brule_pas_le_jeton()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var participant = factory.CreateParticipantClient(invitation.Token);

        var before = await participant.GetFromJsonAsync<ParticipationStatusDto>(
            $"/api/sondages/{surveyId}/participation");
        var beforeAgain = await participant.GetFromJsonAsync<ParticipationStatusDto>(
            $"/api/sondages/{surveyId}/participation");

        Assert.Equal(new ParticipationStatusDto(surveyId, false), before);
        Assert.Equal(new ParticipationStatusDto(surveyId, false), beforeAgain);

        var submitted = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var after = await participant.GetFromJsonAsync<ParticipationStatusDto>(
            $"/api/sondages/{surveyId}/participation");
        Assert.Equal(new ParticipationStatusDto(surveyId, true), after);
    }

    [Fact]
    public async Task Le_statut_exige_un_jeton_de_participation()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, _) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await client.GetAsync($"/api/sondages/{surveyId}/participation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Le_statut_dun_autre_sondage_est_interdit()
    {
        var client = factory.CreateApiKeyClient();
        var (_, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var other = await ApiScenario.CreateSurveyAsync(client, "Sondage voisin");

        var response = await factory.CreateParticipantClient(invitation.Token)
            .GetAsync($"/api/sondages/{other.Id}/participation");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Le_statut_exige_aussi_la_cle_dApi()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var withoutApiKey = factory.CreateClient();
        withoutApiKey.DefaultRequestHeaders.Add(ParticipantTokenDefaults.HeaderName, invitation.Token);

        var response = await withoutApiKey.GetAsync($"/api/sondages/{surveyId}/participation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Clé d'API invalide.", await response.Content.ReadAsStringAsync());
    }
}
