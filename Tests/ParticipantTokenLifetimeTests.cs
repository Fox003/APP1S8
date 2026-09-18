using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Durée de vie d'un jeton. Classe séparée parce que ces tests avancent l'horloge du
/// serveur : les laisser voisiner avec les autres rendrait l'ordre d'exécution
/// significatif.
/// </summary>
public class ParticipantTokenLifetimeTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    [Fact]
    public async Task Un_jeton_expire_est_refuse_en_401()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, validityDays: 1);

        factory.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Trop tard"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await factory.QueryDatabaseAsync(db =>
            db.Responses.CountAsync(r => r.SurveyId == surveyId)));
    }

    /// <summary>
    /// Un jeton expiré et un jeton inconnu doivent être indiscernables : distinguer
    /// les deux confirmerait à un attaquant qu'un jeton a existé.
    /// </summary>
    [Fact]
    public async Task Un_jeton_expire_est_indiscernable_dun_jeton_inconnu()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, validityDays: 1);

        factory.Clock.Advance(TimeSpan.FromDays(2));

        var expired = await factory.CreateParticipantClient(invitation.Token)
            .GetAsync($"/api/sondages/{surveyId}/participation");
        var unknown = await factory.CreateParticipantClient("jeton-jamais-emis")
            .GetAsync($"/api/sondages/{surveyId}/participation");

        Assert.Equal(expired.StatusCode, unknown.StatusCode);
        Assert.Equal(await expired.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// L'expiration est comparée avec <c>&lt;=</c> : à la seconde exacte, le jeton est
    /// déjà mort. Cette borne est la limite d'une fenêtre d'exploitation.
    /// </summary>
    [Fact]
    public async Task Un_jeton_est_refuse_a_la_seconde_exacte_de_son_expiration()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, validityDays: 1);

        factory.Clock.Advance(TimeSpan.FromDays(1));

        var response = await factory.CreateParticipantClient(invitation.Token)
            .GetAsync($"/api/sondages/{surveyId}/participation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Un_jeton_encore_valide_juste_avant_lecheance_est_accepte()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, validityDays: 1);

        factory.Clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));

        var response = await factory.CreateParticipantClient(invitation.Token)
            .GetAsync($"/api/sondages/{surveyId}/participation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Fermeture d'un sondage. Classe séparée pour la même raison : l'horloge avance.
/// </summary>
public class ClosedSurveyTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    [Fact]
    public async Task Un_sondage_ferme_naccepte_plus_de_reponse()
    {
        var client = factory.CreateApiKeyClient();
        var closesAt = factory.Clock.GetUtcNow().AddHours(1);
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, closesAt);

        factory.Clock.Advance(TimeSpan.FromHours(2));

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Trop tard"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Sondage clos", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Le refus doit intervenir avant la rédemption : un sondage clos ne doit pas
    /// brûler la participation de quelqu'un qui pourrait encore répondre ailleurs.
    /// </summary>
    [Fact]
    public async Task Un_sondage_ferme_ne_consomme_pas_le_jeton()
    {
        var client = factory.CreateApiKeyClient();
        var closesAt = factory.Clock.GetUtcNow().AddHours(1);
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, closesAt);

        factory.Clock.Advance(TimeSpan.FromHours(2));

        await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Trop tard"));

        var stored = await factory.QueryDatabaseAsync(db =>
            db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.InvitationId));

        Assert.Null(stored.RedeemedAt);
    }

    /// <summary>La fermeture est comparée avec <c>&lt;=</c> : à l'échéance, c'est fermé.</summary>
    [Fact]
    public async Task Un_sondage_est_clos_a_la_seconde_exacte_de_son_echeance()
    {
        var client = factory.CreateApiKeyClient();
        var closesAt = factory.Clock.GetUtcNow().AddHours(1);
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client, closesAt);

        factory.Clock.Advance(TimeSpan.FromHours(1));

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Pile à l'heure"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
