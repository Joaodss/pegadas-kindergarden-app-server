using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Pegadas.Api.OpenApi;

internal static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    public const string BearerScheme = "Bearer";

    /// <summary>
    /// OpenAPI 3.1 document for <c>/v1</c>. Served at runtime only when <c>OpenApi:Enabled</c>
    /// (Development, Staging) and generated at build time into <c>openapi/v1.json</c>.
    /// </summary>
    public static IServiceCollection AddPegadasOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Pegadas API",
                    Version = DocumentName,
                    Description = "Backend of the Pegadas kindergarten diary. All /v1 endpoints use problem+json errors with a stable `code`.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token from POST /v1/auth/pin (15 minutes).",
                };

                return Task.CompletedTask;
            });

            // The API is secure by default, so every operation needs a token unless it allows anonymous access.
            options.AddOperationTransformer((operation, context, _) =>
            {
                if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                        },
                    ];
                }

                return Task.CompletedTask;
            });
        });
}
