using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using SONDAGEAPI.OpenApi;
using SONDAGEAPI.Security.Participants;

namespace Tests;

/// <summary>
/// Les transformateurs OpenAPI, appelés directement.
/// <para>
/// <see cref="OpenApiDocumentTests"/> vérifie le document tel que l'API le produit
/// aujourd'hui ; ces tests-ci s'attaquent aux cas que le pipeline actuel ne présente
/// pas : un document encore vide, une exigence de sécurité déjà posée, un schéma
/// numérique à virgule. Ce sont des états parfaitement atteignables dès que le
/// document change — un endpoint exposant un <c>double</c>, ou un second
/// transformateur s'exécutant avant celui-ci.
/// </para>
/// </summary>
public class OpenApiTransformerTests
{
    // Aucun des transformateurs de document n'utilise son contexte ; il est tout de
    // même fourni pour appeler l'API telle qu'elle est déclarée.
    private static readonly IServiceProvider EmptyServices = new ServiceCollection().BuildServiceProvider();

    private static OpenApiDocumentTransformerContext DocumentContext() => new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = EmptyServices
    };

    // -----------------------------------------------------------------------
    // Clé d'API : exigence globale
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Le_transformateur_de_cle_dApi_remplit_un_document_encore_vide()
    {
        var document = new OpenApiDocument();

        await new ApiKeySecuritySchemeTransformer()
            .TransformAsync(document, DocumentContext(), CancellationToken.None);

        var scheme = Assert.IsType<OpenApiSecurityScheme>(
            document.Components!.SecuritySchemes![ApiKeySecuritySchemeTransformer.SchemeName]);

        Assert.Equal(SecuritySchemeType.ApiKey, scheme.Type);
        Assert.Equal(ParameterLocation.Header, scheme.In);
        Assert.Equal(ApiKeySecuritySchemeTransformer.HeaderName, scheme.Name);
        Assert.Single(document.Security!);
    }

    /// <summary>
    /// Appelé sur un document déjà pourvu, le transformateur doit compléter et non
    /// écraser : perdre les composants existants supprimerait les schémas de corps.
    /// </summary>
    [Fact]
    public async Task Le_transformateur_de_cle_dApi_preserve_un_document_deja_pourvu()
    {
        var transformer = new ApiKeySecuritySchemeTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        Assert.Single(document.Components!.SecuritySchemes!);
        Assert.Equal(2, document.Security!.Count);
    }

    // -----------------------------------------------------------------------
    // Jeton de participation : schéma global, exigence par opération
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Le_transformateur_de_jeton_declare_son_schema_sur_un_document_vide()
    {
        var document = new OpenApiDocument();

        await new ParticipantTokenSecuritySchemeTransformer()
            .TransformAsync(document, DocumentContext(), CancellationToken.None);

        var scheme = Assert.IsType<OpenApiSecurityScheme>(
            document.Components!.SecuritySchemes![ParticipantTokenSecuritySchemeTransformer.SchemeName]);

        Assert.Equal(ParticipantTokenDefaults.HeaderName, scheme.Name);

        // Aucune exigence au niveau du document : le jeton ne concerne que les
        // opérations de participation.
        Assert.Null(document.Security);
    }

    [Fact]
    public async Task Le_transformateur_de_jeton_preserve_un_document_deja_pourvu()
    {
        var transformer = new ParticipantTokenSecuritySchemeTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        Assert.Single(document.Components!.SecuritySchemes!);
    }

    [Fact]
    public async Task Une_operation_protegee_par_la_politique_recoit_les_deux_schemas()
    {
        var document = new OpenApiDocument();
        var transformer = new ParticipantTokenSecuritySchemeTransformer();
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        var operation = new OpenApiOperation();
        await transformer.TransformAsync(operation,
            OperationContext(document, new AuthorizeAttribute { Policy = ParticipantTokenDefaults.PolicyName }),
            CancellationToken.None);

        var requirement = Assert.Single(operation.Security!);
        Assert.Equal(2, requirement.Count);
    }

    [Fact]
    public async Task Une_operation_sans_la_politique_nest_pas_touchee()
    {
        var document = new OpenApiDocument();
        var operation = new OpenApiOperation();

        await new ParticipantTokenSecuritySchemeTransformer().TransformAsync(operation,
            OperationContext(document, new AuthorizeAttribute { Policy = "UneAutrePolitique" }),
            CancellationToken.None);

        Assert.Null(operation.Security);
    }

    /// <summary>
    /// Une opération portant déjà une exigence doit conserver la sienne : l'écraser
    /// relâcherait silencieusement une contrainte de sécurité posée ailleurs.
    /// </summary>
    [Fact]
    public async Task Une_exigence_deja_posee_sur_loperation_est_conservee()
    {
        var document = new OpenApiDocument();
        var transformer = new ParticipantTokenSecuritySchemeTransformer();
        await transformer.TransformAsync(document, DocumentContext(), CancellationToken.None);

        var operation = new OpenApiOperation { Security = [new OpenApiSecurityRequirement()] };
        var context = OperationContext(document,
            new AuthorizeAttribute { Policy = ParticipantTokenDefaults.PolicyName });

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.Equal(2, operation.Security!.Count);
    }

    // -----------------------------------------------------------------------
    // Types numériques : suppression des unions integer|string
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Une_union_entier_ou_chaine_est_ramenee_a_un_entier()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer | JsonSchemaType.String,
            Pattern = @"^-?\d+$"
        };

        await TransformSchemaAsync(schema);

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Null(schema.Pattern);
    }

    [Fact]
    public async Task Une_union_decimal_ou_chaine_est_ramenee_a_un_nombre()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Number | JsonSchemaType.String,
            Pattern = @"^-?\d+(\.\d+)?$"
        };

        await TransformSchemaAsync(schema);

        Assert.Equal(JsonSchemaType.Number, schema.Type);
        Assert.Null(schema.Pattern);
    }

    /// <summary>Une chaîne reste une chaîne, motif de validation compris.</summary>
    [Fact]
    public async Task Une_chaine_simple_nest_pas_touchee()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Pattern = "^[a-z]+$"
        };

        await TransformSchemaAsync(schema);

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("^[a-z]+$", schema.Pattern);
    }

    [Fact]
    public async Task Un_type_sans_composante_chaine_nest_pas_touche()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Pattern = "^inchangé$" };

        await TransformSchemaAsync(schema);

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Equal("^inchangé$", schema.Pattern);
    }

    [Fact]
    public async Task Un_schema_sans_type_declare_nest_pas_touche()
    {
        var schema = new OpenApiSchema { Type = null, Pattern = "^inchangé$" };

        await TransformSchemaAsync(schema);

        Assert.Null(schema.Type);
        Assert.Equal("^inchangé$", schema.Pattern);
    }

    private static Task TransformSchemaAsync(OpenApiSchema schema) =>
        new NumericUnionSchemaTransformer().TransformAsync(schema, SchemaContext(), CancellationToken.None);

    private static OpenApiSchemaTransformerContext SchemaContext() => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(int)),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = EmptyServices
    };

    private static OpenApiOperationTransformerContext OperationContext(
        OpenApiDocument document, params object[] endpointMetadata) => new()
    {
        DocumentName = "v1",
        ApplicationServices = EmptyServices,
        Document = document,
        Description = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata }
        }
    };
}
