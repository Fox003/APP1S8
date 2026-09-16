using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SONDAGEAPI.OpenApi;

// Le générateur type un Int comme Integer|String : ça accepte aussi bien 30 que "30".
// En OpenAPI 3.1 cela se serialise en type: ["integer","string"]
// En OpenAPI 3.0 devient un anyOf — un type union, mal supporté.
//
// À noter : le nettoyage doit viser JsonSchemaType (un enum [Flags]) et non AnyOf.
// Au moment ou les transformateurs s'exécutent, le document est encore en 3.1 et
// AnyOf est vide ; l'anyOf n'apparait qu'à la sérialisation.
internal sealed class NumericUnionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is not { } type || !type.HasFlag(JsonSchemaType.String))
        {
            return Task.CompletedTask;
        }

        if (type.HasFlag(JsonSchemaType.Integer))
        {
            schema.Type = JsonSchemaType.Integer;
        }
        else if (type.HasFlag(JsonSchemaType.Number))
        {
            schema.Type = JsonSchemaType.Number;
        }
        else
        {
            return Task.CompletedTask;
        }

        schema.Pattern = null;

        return Task.CompletedTask;
    }
}
