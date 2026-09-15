using SONDAGEAPI.OpenApi;

namespace Microsoft.Extensions.DependencyInjection;

public static class OpenApiExtensions
{
    public static IServiceCollection AddSondageOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
            options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>());
        return services;
    }

    public static WebApplication UseSondageOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SONDAGEAPI v1"));
        }

        return app;
    }
}