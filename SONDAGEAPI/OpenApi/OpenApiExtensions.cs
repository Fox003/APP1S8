using Microsoft.OpenApi;
using SONDAGEAPI.OpenApi;

namespace Microsoft.Extensions.DependencyInjection;

public static class OpenApiExtensions
{
    public static IServiceCollection AddSondageOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            // Swagger UI et Postman supportent mal OpenAPI 3.1 (types en union).
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>();
            options.AddDocumentTransformer<ParticipantTokenSecuritySchemeTransformer>();
            options.AddOperationTransformer<ParticipantTokenSecuritySchemeTransformer>();
        });
        return services;
    }

    public static WebApplication UseSondageOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithoutApiKey();   // endpoint: s'exécute après le middleware, donc à exempter
            app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SONDAGEAPI v1"));
        }

        return app;
    }
}