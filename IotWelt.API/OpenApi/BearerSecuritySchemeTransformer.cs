using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IotWelt.API.OpenApi;

// Beschreibt im OpenAPI-Dokument, dass die API Bearer-JWTs erwartet. Ohne dieses Schema bietet Scalar
// kein Feld für das Token an, und der Authorization-Header müsste von Hand gesetzt werden.
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access-Token aus POST /api/auth/login (accessToken)",
        };

        // Global für alle Operationen; anonyme Endpoints (Login, Sensor) ignorieren den Header einfach
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] },
        ];
        return Task.CompletedTask;
    }
}
