using System.Net;
using System.Text.Json;
using SONDAGEAPI.OpenApi;
using SONDAGEAPI.Security;
using SONDAGEAPI.Security.Participants;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Livrable 2 — le document OpenAPI produit réellement par l'API. Il sert de source
/// à la collection Postman du livrable 3, donc ce qu'il déclare doit correspondre à
/// ce que le pipeline exige : sinon un client suivant la documentation se fait
/// refuser en 401 sans comprendre pourquoi.
/// </summary>
public class OpenApiDocumentTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>, IAsyncLifetime
{
    private JsonElement _document;

    public async Task InitializeAsync()
    {
        var response = await factory.CreateApiKeyClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// La version est épinglée à 3.0 : Swagger UI et Postman supportent mal les types
    /// en union de 3.1.
    /// </summary>
    [Fact]
    public void Le_document_est_serialise_en_OpenAPI_3_0()
    {
        Assert.StartsWith("3.0", _document.GetProperty("openapi").GetString());
    }

    [Fact]
    public void Le_schema_de_securite_de_la_cle_dApi_est_declare()
    {
        var scheme = _document.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(ApiKeySecuritySchemeTransformer.SchemeName);

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal(ApiKeyOptions.HeaderName, scheme.GetProperty("name").GetString());
    }

    [Fact]
    public void Le_schema_de_securite_du_jeton_de_participation_est_declare()
    {
        var scheme = _document.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(ParticipantTokenSecuritySchemeTransformer.SchemeName);

        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal(ParticipantTokenDefaults.HeaderName, scheme.GetProperty("name").GetString());
    }

    /// <summary>La clé d'API vaut pour toute l'API : exigence au niveau du document.</summary>
    [Fact]
    public void La_cle_dApi_est_exigee_globalement()
    {
        var requirements = _document.GetProperty("security").EnumerateArray().ToList();

        Assert.Contains(requirements, r =>
            r.TryGetProperty(ApiKeySecuritySchemeTransformer.SchemeName, out _));
    }

    /// <summary>
    /// En OpenAPI 3.0, la sécurité déclarée sur une opération REMPLACE celle du
    /// document. Les opérations de participation doivent donc redéclarer la clé
    /// d'API en plus du jeton, sinon Swagger génère des requêtes sans X-API-Key.
    /// </summary>
    [Theory]
    [InlineData("/api/sondages/{id}/reponses", "post")]
    [InlineData("/api/sondages/{id}/participation", "get")]
    public void Les_operations_de_participation_exigent_les_deux_schemas_ensemble(string path, string method)
    {
        var requirements = _document.GetProperty("paths").GetProperty(path)
            .GetProperty(method).GetProperty("security").EnumerateArray().ToList();

        Assert.Contains(requirements, r =>
            r.TryGetProperty(ApiKeySecuritySchemeTransformer.SchemeName, out _) &&
            r.TryGetProperty(ParticipantTokenSecuritySchemeTransformer.SchemeName, out _));
    }

    [Theory]
    [InlineData("/api/sondages", "post")]
    [InlineData("/api/sondages/{id}/invitations", "post")]
    public void Les_operations_dadministration_nexigent_pas_de_jeton_de_participation(string path, string method)
    {
        var operation = _document.GetProperty("paths").GetProperty(path).GetProperty(method);

        Assert.False(operation.TryGetProperty("security", out _));
    }

    /// <summary>
    /// Le générateur type un int comme union integer|string, qui se sérialise en
    /// anyOf en 3.0 — mal supporté par Postman. Le transformateur le ramène à un
    /// type simple.
    /// </summary>
    [Fact]
    public void Les_entiers_ne_sont_pas_serialises_en_union_de_types()
    {
        var schema = _document.GetProperty("components").GetProperty("schemas")
            .GetProperty("CreateInvitationRequest").GetProperty("properties")
            .GetProperty("validityDays");

        Assert.Equal("integer", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("anyOf", out _));
        Assert.False(schema.TryGetProperty("pattern", out _));
    }

    [Fact]
    public async Task Le_document_OpenAPI_est_lisible_sans_cle_dApi()
    {
        // MapOpenApi est marqué WithoutApiKey : sans cela, Swagger UI ne pourrait pas
        // charger le schéma et l'interface serait vide.
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_UI_est_servie_en_Development()
    {
        var response = await factory.CreateApiKeyClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
