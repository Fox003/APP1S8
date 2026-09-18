using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Livrable 1 — le trafic doit passer par HTTPS. Une clé d'API circulant en clair
/// serait lisible par n'importe quel intermédiaire réseau ; la redirection est donc
/// la première ligne de défense, avant même le middleware.
/// </summary>
public class HttpsRedirectionTests(HttpsRedirectionApiFactory factory)
    : IClassFixture<HttpsRedirectionApiFactory>
{
    // Suivre la redirection masquerait ce qu'on cherche à observer.
    private static readonly WebApplicationFactoryClientOptions DoNotFollow = new() { AllowAutoRedirect = false };

    [Fact]
    public async Task Une_requete_HTTP_est_redirigee_vers_HTTPS()
    {
        var response = await factory.CreateClient(DoNotFollow).GetAsync("/ping");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location?.Scheme);
        Assert.Equal(HttpsRedirectionApiFactory.HttpsPort, response.Headers.Location?.Port);
    }

    /// <summary>
    /// La redirection précède le middleware de clé d'API : un appel en clair n'est
    /// pas traité, même muni d'une clé valide — mais la clé, elle, a déjà voyagé en
    /// clair. La redirection protège la requête suivante, pas celle-ci ; c'est HSTS,
    /// côté client, qui protège la première. À retenir pour les livrables 6 et 9.
    /// </summary>
    [Fact]
    public async Task La_redirection_precede_la_verification_de_la_cle_dApi()
    {
        var response = await factory.CreateApiKeyClient(DoNotFollow)
            .GetAsync($"/api/sondages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
