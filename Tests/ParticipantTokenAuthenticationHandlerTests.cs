using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SONDAGEAPI.Data;
using SONDAGEAPI.Security.Participants;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Les réponses de rejet du gestionnaire, testées directement.
/// <para>
/// <c>HandleForbiddenAsync</c> n'est pas atteignable par une requête HTTP dans l'API
/// actuelle : la politique n'exige que la présence de <c>invitation_id</c>, et le
/// gestionnaire l'ajoute toujours quand il réussit. Le jour où une exigence
/// s'ajoute à la politique, ou où le schéma JWT est composé avec celui-ci, ce chemin
/// devient vivant — il doit donc déjà être correct, et vérifié.
/// </para>
/// </summary>
public class ParticipantTokenAuthenticationHandlerTests
{
    [Fact]
    public async Task Un_refus_dautorisation_produit_un_403_en_ProblemDetails()
    {
        var (handler, context) = await CreateHandlerAsync();

        await handler.ForbidAsync(properties: null);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType?.Split(';')[0]);

        var body = ReadBody(context);
        Assert.Contains("Interdit", body);
        Assert.Contains("Ce jeton ne donne pas accès à cette ressource.", body);
    }

    [Fact]
    public async Task Un_defi_dauthentification_produit_un_401_en_ProblemDetails()
    {
        var (handler, context) = await CreateHandlerAsync();

        await handler.ChallengeAsync(properties: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        var body = ReadBody(context);
        Assert.Contains("Non autorisé", body);
        Assert.Contains("Jeton de participation absent ou invalide.", body);
    }

    /// <summary>
    /// Le message de refus ne doit rien dire du jeton présenté : ni sa valeur, ni la
    /// raison exacte du rejet.
    /// </summary>
    [Fact]
    public async Task Le_message_de_refus_ne_reprend_jamais_le_jeton_presente()
    {
        const string presented = "jeton-secret-du-participant";
        var (handler, context) = await CreateHandlerAsync(presented);

        await handler.ChallengeAsync(properties: null);

        Assert.DoesNotContain(presented, ReadBody(context));
    }

    private static async Task<(ParticipantTokenAuthenticationHandler Handler, HttpContext Context)>
        CreateHandlerAsync(string? presentedToken = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        services.AddOptions();
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Response.Body = new MemoryStream();

        if (presentedToken is not null)
        {
            context.Request.Headers[ParticipantTokenDefaults.HeaderName] = presentedToken;
        }

        // Le gestionnaire exige un DbContext, mais les chemins testés ici ne
        // l'interrogent pas : une base jamais ouverte suffit.
        var database = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);

        var handler = new ParticipantTokenAuthenticationHandler(
            provider.GetRequiredService<IOptionsMonitor<AuthenticationSchemeOptions>>(),
            provider.GetRequiredService<ILoggerFactory>(),
            UrlEncoder.Default,
            database,
            new TestTimeProvider(DateTimeOffset.UnixEpoch));

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ParticipantTokenDefaults.AuthenticationScheme,
                displayName: null,
                typeof(ParticipantTokenAuthenticationHandler)),
            context);

        return (handler, context);
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
    }
}
