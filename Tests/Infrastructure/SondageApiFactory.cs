using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SONDAGEAPI.Data;
using SONDAGEAPI.Security;
using SONDAGEAPI.Security.Participants;

namespace Tests.Infrastructure;

/// <summary>
/// Héberge l'API réelle en mémoire (TestServer) : le pipeline complet est exercé,
/// middleware de clé d'API et schéma d'authentification compris. Deux seules choses
/// sont substituées, et pour une raison précise :
/// <list type="bullet">
///   <item>la base SQLite, déplacée dans un fichier temporaire unique par classe de
///   test — un vrai SQLite et non le fournisseur InMemory, parce que la garantie
///   d'unicité repose sur un <c>UPDATE</c> conditionnel et une transaction, que le
///   fournisseur InMemory ne sait pas reproduire ;</item>
///   <item>le <see cref="TimeProvider"/>, pour rendre l'expiration testable.</item>
/// </list>
/// </summary>
public class SondageApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Clé valide du test : 40 caractères, donc conforme au minimum de 32.</summary>
    public const string ValidApiKey = "cle-api-de-test-0123456789-0123456789-ab";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), "sondageapi-tests", $"{Guid.NewGuid():N}.db");

    /// <summary>Horloge du serveur ; avancer d'ici modifie ce que voit l'API.</summary>
    public TestTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Development câble Swagger et /openapi ; Production ne les câble pas. Les deux
    /// branches de <c>UseSondageOpenApi</c> doivent être exercées.
    /// </summary>
    public string EnvironmentName { get; protected init; } = Environments.Development;

    private string ConnectionString => $"Data Source={_databasePath}";

    /// <summary>Point d'extension pour les variantes (jeton falsifié, port HTTPS…).</summary>
    protected virtual void RegisterTestServices(IServiceCollection services) { }

    protected virtual void RegisterTestSettings(IDictionary<string, string?> settings) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Ajoutée en dernier, donc prioritaire sur appsettings.json et sur le
            // magasin de secrets du développeur : la suite ne dépend d'aucune
            // configuration locale.
            var settings = new Dictionary<string, string?>
            {
                ["Sondage:ApiKey"] = ValidApiKey,
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            };

            RegisterTestSettings(settings);
            configuration.AddInMemoryCollection(settings);
        });

        // ConfigureTestServices et non ConfigureServices : seul le premier s'exécute
        // après les enregistrements de Program.cs, donc seul lui peut remplacer le
        // TimeProvider posé par TryAddSingleton.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            RegisterTestServices(services);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();

        return host;
    }

    /// <summary>Client porteur d'une clé d'API valide : le cas nominal.</summary>
    public HttpClient CreateApiKeyClient(WebApplicationFactoryClientOptions? options = null)
    {
        var client = options is null ? CreateClient() : CreateClient(options);
        client.DefaultRequestHeaders.Add(ApiKeyOptions.HeaderName, ValidApiKey);
        return client;
    }

    /// <summary>Client porteur de la clé d'API et d'un jeton de participation.</summary>
    public HttpClient CreateParticipantClient(string participantToken)
    {
        var client = CreateApiKeyClient();
        client.DefaultRequestHeaders.Add(ParticipantTokenDefaults.HeaderName, participantToken);
        return client;
    }

    /// <summary>Accès direct à la base pour vérifier l'état persisté après une requête.</summary>
    public async Task<T> QueryDatabaseAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>
    /// Exécute du SQL brut avec les clés étrangères désactivées, et rend le nombre de
    /// lignes touchées. Sert à fabriquer un état que l'API seule ne peut pas produire
    /// (invitation orpheline), afin d'exercer les gardes défensives des endpoints.
    /// Les valeurs passent par des paramètres, comme partout ailleurs.
    /// </summary>
    public async Task<int> ExecuteWithoutForeignKeysAsync(
        string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        await pragma.ExecuteNonQueryAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // ClearPool et non ClearAllPools : le pool est global au processus, et le vider
        // entièrement couperait les connexions des autres classes de test qui tournent
        // en parallèle. On ne libère que le fichier de cette instance.
        SqliteConnection.ClearPool(new SqliteConnection(ConnectionString));

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Fichier temporaire : un verrou résiduel n'a pas à faire échouer un test.
        }
    }
}
