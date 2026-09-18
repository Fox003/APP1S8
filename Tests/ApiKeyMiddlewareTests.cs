using System.Net;
using System.Net.Http.Json;
using SONDAGEAPI.Contracts;
using SONDAGEAPI.Security;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Livrable 1 — la clé d'API autorise l'accès à l'API.
/// Vecteurs d'attaque couverts : appel non authentifié, clé devinée, oracle
/// d'erreur permettant de distinguer « pas de clé » de « mauvaise clé »,
/// contrebande d'en-tête par duplication.
/// </summary>
public class ApiKeyMiddlewareTests(SondageApiFactory factory) : IClassFixture<SondageApiFactory>
{
    private const string ProtectedRoute = "/api/sondages/11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Ping_est_exempte_de_cle_dApi()
    {
        var response = await factory.CreateClient().GetAsync("/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PingPayload>();
        Assert.Equal("pong", payload!.Status);
    }

    [Fact]
    public async Task Sans_en_tete_la_requete_est_refusee()
    {
        var response = await factory.CreateClient().GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Avec_une_mauvaise_cle_la_requete_est_refusee()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyOptions.HeaderName, "cle-invalide-mais-de-longueur-correcte-42");

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Une_cle_vide_ou_blanche_est_refusee(string value)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(ApiKeyOptions.HeaderName, value);

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// En-tête dupliqué : une pile intermédiaire pourrait ne lire que la première
    /// valeur et l'API la seconde. Le middleware refuse tout ce qui n'est pas
    /// exactement une valeur, y compris quand l'une des deux est la bonne clé.
    /// </summary>
    [Fact]
    public async Task Un_en_tete_duplique_est_refuse_meme_si_une_valeur_est_bonne()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            ApiKeyOptions.HeaderName, new[] { SondageApiFactory.ValidApiKey, "valeur-injectee" });

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Pas d'oracle : si l'absence de clé et une clé fausse produisaient des réponses
    /// différentes, un attaquant saurait quand il tient un format valide.
    /// </summary>
    [Fact]
    public async Task Les_refus_sont_indiscernables_entre_cle_absente_et_cle_fausse()
    {
        var withoutKey = factory.CreateClient();

        var withWrongKey = factory.CreateClient();
        withWrongKey.DefaultRequestHeaders.Add(ApiKeyOptions.HeaderName, "une-autre-cle-de-32-caracteres-ou-plus");

        var first = await withoutKey.GetAsync(ProtectedRoute);
        var second = await withWrongKey.GetAsync(ProtectedRoute);

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(first.Content.Headers.ContentType?.MediaType, second.Content.Headers.ContentType?.MediaType);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Avec_la_bonne_cle_la_requete_atteint_lendpoint()
    {
        var response = await factory.CreateApiKeyClient().GetAsync(ProtectedRoute);

        // 404 et non 401 : la clé a été acceptée, c'est le sondage qui n'existe pas.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task La_reponse_401_est_un_ProblemDetails_sans_detail_exploitable()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/sondages", new CreateSurveyRequest("Sondage", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Clé d'API invalide.", body);
        Assert.DoesNotContain(SondageApiFactory.ValidApiKey, body);
    }

    private sealed record PingPayload(string Status, DateTime Timestamp);
}
