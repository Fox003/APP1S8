using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SONDAGEAPI.Security;

namespace SONDAGEAPI.OpenApi;

internal sealed class ApiKeySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "ApiKeyAuth";
    public const string HeaderName = ApiKeyOptions.HeaderName;

    public Task TransformAsync(OpenApiDocument document, 
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = HeaderName,
            Description = "Clé d'API requise."
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = new List<string>()
        });
        
        return Task.CompletedTask;
    }
}