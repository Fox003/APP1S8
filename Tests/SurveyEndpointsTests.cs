using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using SONDAGEAPI.Models;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Administration des sondages. Au-delà du chemin nominal, ces tests couvrent la
/// validation d'entrée : longueur du titre (charge utile démesurée), date de
/// fermeture déjà passée (sondage mort-né), et injection SQL.
/// </summary>
public class SurveyEndpointsTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    [Fact]
    public async Task Un_sondage_valide_est_cree_avec_201_et_un_entete_Location()
    {
        var client = factory.CreateApiKeyClient();
        var closesAt = factory.Clock.GetUtcNow().AddDays(7);

        var response = await client.PostAsJsonAsync("/api/sondages",
            new CreateSurveyRequest("Satisfaction du cours", closesAt));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SurveyDto>();
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("Satisfaction du cours", created.Title);
        Assert.Equal(factory.Clock.GetUtcNow(), created.CreatedAt);
        Assert.Equal(closesAt, created.ClosesAt);
        Assert.Equal($"/api/sondages/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Un_sondage_sans_date_de_fermeture_reste_ouvert()
    {
        var created = await ApiScenario.CreateSurveyAsync(factory.CreateApiKeyClient(), "Sondage perpétuel");

        Assert.Null(created.ClosesAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Un_titre_vide_est_refuse(string title)
    {
        var response = await factory.CreateApiKeyClient()
            .PostAsJsonAsync("/api/sondages", new CreateSurveyRequest(title, null));

        await AssertValidationProblemAsync(response, "title");
    }

    [Fact]
    public async Task Un_titre_plus_long_que_la_limite_est_refuse()
    {
        var title = new string('a', Survey.TitleMaxLength + 1);

        var response = await factory.CreateApiKeyClient()
            .PostAsJsonAsync("/api/sondages", new CreateSurveyRequest(title, null));

        await AssertValidationProblemAsync(response, "title");
    }

    [Fact]
    public async Task Un_titre_exactement_a_la_limite_est_accepte()
    {
        var title = new string('a', Survey.TitleMaxLength);

        var created = await ApiScenario.CreateSurveyAsync(factory.CreateApiKeyClient(), title);

        Assert.Equal(Survey.TitleMaxLength, created.Title.Length);
    }

    [Fact]
    public async Task Le_titre_est_debarrasse_de_ses_espaces_de_bordure()
    {
        var created = await ApiScenario.CreateSurveyAsync(factory.CreateApiKeyClient(), "  Titre espacé  ");

        Assert.Equal("Titre espacé", created.Title);
    }

    [Fact]
    public async Task Une_date_de_fermeture_deja_passee_est_refusee()
    {
        var response = await factory.CreateApiKeyClient().PostAsJsonAsync("/api/sondages",
            new CreateSurveyRequest("Sondage mort-né", factory.Clock.GetUtcNow().AddMinutes(-1)));

        await AssertValidationProblemAsync(response, "closesAt");
    }

    [Fact]
    public async Task Une_date_de_fermeture_egale_a_maintenant_est_refusee()
    {
        var response = await factory.CreateApiKeyClient().PostAsJsonAsync("/api/sondages",
            new CreateSurveyRequest("Sondage instantané", factory.Clock.GetUtcNow()));

        await AssertValidationProblemAsync(response, "closesAt");
    }

    /// <summary>
    /// EF Core paramètre les requêtes : la charge utile doit ressortir telle quelle et
    /// les tables doivent survivre. Un titre concaténé dans du SQL aurait fait tomber
    /// la table Surveys, et les lectures suivantes échoueraient.
    /// </summary>
    [Fact]
    public async Task Un_titre_portant_une_injection_SQL_est_stocke_litteralement()
    {
        const string payload = "'); DROP TABLE Surveys;--";
        var client = factory.CreateApiKeyClient();

        var created = await ApiScenario.CreateSurveyAsync(client, payload);
        var reread = await client.GetFromJsonAsync<SurveyDto>($"/api/sondages/{created.Id}");

        Assert.Equal(payload, reread!.Title);
        Assert.True(await factory.QueryDatabaseAsync(db => db.Surveys.AnyAsync()));
    }

    [Fact]
    public async Task Un_sondage_existant_est_relu_a_lidentique()
    {
        var client = factory.CreateApiKeyClient();
        var created = await ApiScenario.CreateSurveyAsync(client, "Sondage relu");

        var response = await client.GetAsync($"/api/sondages/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.Content.ReadFromJsonAsync<SurveyDto>());
    }

    [Fact]
    public async Task Un_sondage_inconnu_retourne_404()
    {
        var unknownId = Guid.NewGuid();

        var response = await factory.CreateApiKeyClient().GetAsync($"/api/sondages/{unknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(unknownId.ToString(), await response.Content.ReadAsStringAsync());
    }

    /// <summary>La contrainte de route :guid rejette avant même d'atteindre le code.</summary>
    [Fact]
    public async Task Un_identifiant_qui_nest_pas_un_guid_ne_correspond_a_aucune_route()
    {
        var response = await factory.CreateApiKeyClient().GetAsync("/api/sondages/pas-un-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Un corps JSON illisible doit donner 400, et non 500. Le contenu de la réponse
    /// est vérifié ailleurs : en Development la page d'exception du développeur y
    /// place une trace d'exécution complète, ce qui est acceptable localement mais
    /// jamais en production. Voir <see cref="ProductionPipelineTests"/>.
    /// </summary>
    [Fact]
    public async Task Un_corps_JSON_malforme_retourne_400_et_non_500()
    {
        var content = new StringContent("{ \"title\": ", Encoding.UTF8, "application/json");

        var response = await factory.CreateApiKeyClient().PostAsync("/api/sondages", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string expectedField)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.True(problem!.Errors.ContainsKey(expectedField));
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
