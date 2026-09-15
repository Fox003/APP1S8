using SONDAGEAPI.Security;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApiKeyExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();     // Pas de clé => Aucune exécution

        return services;
    }

    public static WebApplication UseApiKeyAuthentication(this WebApplication app)
    {
        app.UseMiddleware<ApiKeyMiddleware>();
        return app;
    }
    
    public static TBuilder WithoutApiKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AllowAnonymousApiKeyAttribute());
}