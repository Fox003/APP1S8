using System.Net;
using System.Net.Http.Json;
using System.Text;
using SONDAGEAPI.Contracts;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Le pipeline hors Development. Deux exigences de sécurité y sont vérifiées :
/// le schéma OpenAPI et Swagger ne doivent pas être publiés, et aucune trace
/// d'exécution ne doit sortir de l'API.
/// </summary>
public class ProductionPipelineTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task Le_schema_OpenAPI_nest_pas_publie_hors_Development()
    {
        var response = await factory.CreateApiKeyClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_nest_pas_publie_hors_Development()
    {
        var response = await factory.CreateApiKeyClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Sans la page d'exception du développeur, un corps JSON malformé ne doit rien
    /// révéler de la structure interne : ni trace d'exécution, ni nom de type.
    /// </summary>
    [Fact]
    public async Task Un_corps_JSON_malforme_ne_divulgue_aucune_trace_dexecution()
    {
        var content = new StringContent("{ \"title\": ", Encoding.UTF8, "application/json");

        var response = await factory.CreateApiKeyClient().PostAsync("/api/sondages", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("at SONDAGEAPI", body);
        Assert.DoesNotContain("BadHttpRequestException", body);
    }

    /// <summary>Le reste de l'API se comporte à l'identique en production.</summary>
    [Fact]
    public async Task Les_endpoints_metier_fonctionnent_a_lidentique()
    {
        var client = factory.CreateApiKeyClient();
        var (surveyId, invitation) = await ApiScenario.CreateInvitedParticipantAsync(client);

        var response = await factory.CreateParticipantClient(invitation.Token)
            .PostAsJsonAsync($"/api/sondages/{surveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task La_cle_dApi_reste_exigee_hors_Development()
    {
        var response = await factory.CreateClient().GetAsync($"/api/sondages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
