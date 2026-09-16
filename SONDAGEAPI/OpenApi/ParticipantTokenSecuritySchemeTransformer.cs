using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SONDAGEAPI.Security.Participants;

namespace SONDAGEAPI.OpenApi;

// La clé d'API est une exigence globale (middleware sur toute l'API), le jeton de
// participation ne l'est pas : il ne concerne que les endpoints de participation.
// D'où la déclaration du schéma au niveau du document, mais l'exigence par opération.
internal sealed class ParticipantTokenSecuritySchemeTransformer
    : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string SchemeName = "ParticipantTokenAuth";

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
            Name = ParticipantTokenDefaults.HeaderName,
            Description = "Jeton de participation, requis EN PLUS de la clé d'API."
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requiresToken = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any(data => data.Policy == ParticipantTokenDefaults.PolicyName);

        if (!requiresToken)
        {
            return Task.CompletedTask;
        }

        // La sécurité déclarée sur une opération REMPLACE celle du document (spec OpenAPI 3.0),
        // elle ne s'y ajoute pas. Il faut donc redéclarer la clé d'API ici : sans cela Swagger
        // génère des requêtes sans X-API-Key, que le middleware du livrable 1 rejette en 401.
        // Deux schémas dans UNE même exigence = les deux sont requis.
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiKeySecuritySchemeTransformer.SchemeName, context.Document)] = new List<string>(),
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = new List<string>()
        });

        return Task.CompletedTask;
    }
}
