using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SONDAGEAPI.Security;

namespace Tests;

/// <summary>
/// La clé d'API est validée au démarrage. Un déploiement sans clé, ou avec une clé
/// trop courte, doit faire échouer le démarrage plutôt que servir une API ouverte :
/// échouer bruyamment vaut mieux qu'échouer discrètement.
/// </summary>
public class ApiKeyOptionsTests
{
    [Fact]
    public void Une_cle_absente_fait_echouer_la_validation()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(configuredKey: null));
    }

    [Fact]
    public void Une_cle_vide_fait_echouer_la_validation()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(configuredKey: ""));
    }

    [Fact]
    public void Une_cle_de_moins_de_32_caracteres_fait_echouer_la_validation()
    {
        var exception = Assert.Throws<OptionsValidationException>(
            () => Resolve(new string('a', 31)));

        Assert.Contains("32", exception.Message);
    }

    [Fact]
    public void Une_cle_de_32_caracteres_est_acceptee()
    {
        var key = new string('a', 32);

        Assert.Equal(key, Resolve(key).ApiKey);
    }

    [Fact]
    public void La_section_et_len_tete_sont_ceux_annonces_par_la_documentation()
    {
        Assert.Equal("Sondage", ApiKeyOptions.SectionName);
        Assert.Equal("X-API-Key", ApiKeyOptions.HeaderName);
    }

    private static ApiKeyOptions Resolve(string? configuredKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ApiKeyOptions.SectionName}:ApiKey"] = configuredKey
            })
            .Build();

        var provider = new ServiceCollection()
            .AddApiKeyAuthentication(configuration)
            .BuildServiceProvider();

        return provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
    }
}
