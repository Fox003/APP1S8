using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Le test central du livrable 4, et la mitigation la plus difficile à prouver :
/// la course TOCTOU.
/// <para>
/// Un contrôle « si le jeton n'a pas servi, alors insérer » laisse une fenêtre entre
/// la lecture et l'écriture. N requêtes simultanées peuvent toutes lire
/// <c>RedeemedAt == null</c> avant qu'aucune n'écrive, et toutes passer : l'unicité
/// de participation tombe. L'implémentation place la condition à l'intérieur du
/// <c>UPDATE</c>, donc c'est le moteur de base de données qui arbitre et le nombre de
/// lignes affectées désigne le gagnant.
/// </para>
/// <para>
/// Ces tests ont besoin d'un vrai SQLite : le fournisseur InMemory d'EF Core ne sait
/// reproduire ni le verrouillage, ni <c>ExecuteUpdate</c>, ni la transaction.
/// </para>
/// </summary>
public class ParticipationUniquenessTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    private const int ConcurrentSubmissions = 10;

    [Fact]
    public async Task Dix_soumissions_simultanees_du_meme_jeton_nen_laissent_passer_quune()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        // Un client par requête : partager un HttpClient ne changerait rien au
        // parallélisme côté serveur, mais chaque requête est ainsi indépendante.
        var attempts = Enumerable.Range(0, ConcurrentSubmissions).Select(i =>
            factory.CreateParticipantClient(invitation.Token).PostAsJsonAsync(
                $"/api/sondages/{surveyId}/reponses",
                new SubmitResponseRequest($"Réponse concurrente {i}")));

        var responses = await Task.WhenAll(attempts);

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, created);
        Assert.Equal(ConcurrentSubmissions - 1, conflicts);

        // Aucune autre issue : pas de 500, pas de blocage SQLite remonté au client.
        Assert.All(responses, r => Assert.Contains(
            r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
    }

    [Fact]
    public async Task La_course_ne_laisse_quune_ligne_en_base()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        await Task.WhenAll(Enumerable.Range(0, ConcurrentSubmissions).Select(i =>
            factory.CreateParticipantClient(invitation.Token).PostAsJsonAsync(
                $"/api/sondages/{surveyId}/reponses",
                new SubmitResponseRequest($"Réponse concurrente {i}"))));

        var responseCount = await factory.QueryDatabaseAsync(db =>
            db.Responses.CountAsync(r => r.SurveyId == surveyId));
        var storedInvitation = await factory.QueryDatabaseAsync(db =>
            db.Invitations.AsNoTracking().SingleAsync(i => i.Id == invitation.InvitationId));

        Assert.Equal(1, responseCount);
        Assert.NotNull(storedInvitation.RedeemedAt);
    }

    /// <summary>
    /// La rédemption et l'enregistrement de la réponse partagent une transaction :
    /// un conflit ne doit donc jamais laisser une invitation consommée sans réponse.
    /// </summary>
    [Fact]
    public async Task Le_perdant_de_la_course_ne_laisse_aucune_trace()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);
        var participant = factory.CreateParticipantClient(invitation.Token);

        await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Gagnante"));

        var loser = await participant.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Perdante"));

        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);

        var contents = await factory.QueryDatabaseAsync(db =>
            db.Responses.AsNoTracking().Where(r => r.SurveyId == surveyId)
                .Select(r => r.Content).ToListAsync());

        Assert.Equal(["Gagnante"], contents);
    }

    /// <summary>
    /// Deux participants distincts du même sondage ne se gênent pas : l'unicité porte
    /// sur l'invitation, pas sur le sondage.
    /// </summary>
    [Fact]
    public async Task Deux_participants_distincts_peuvent_repondre_au_meme_sondage()
    {
        var client = factory.CreateApiKeyClient();
        var survey = await ApiScenario.CreateSurveyAsync(client, "Sondage à deux voix");
        var first = await ApiScenario.CreateInvitationAsync(client, survey.Id);
        var second = await ApiScenario.CreateInvitationAsync(client, survey.Id);

        var firstResponse = await factory.CreateParticipantClient(first.Token).PostAsJsonAsync(
            $"/api/sondages/{survey.Id}/reponses", new SubmitResponseRequest("Avis du premier"));
        var secondResponse = await factory.CreateParticipantClient(second.Token).PostAsJsonAsync(
            $"/api/sondages/{survey.Id}/reponses", new SubmitResponseRequest("Avis du second"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(2, await factory.QueryDatabaseAsync(db =>
            db.Responses.CountAsync(r => r.SurveyId == survey.Id)));
    }
}
