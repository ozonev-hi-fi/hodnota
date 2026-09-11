using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hodnota.Api.OpenApi;

public sealed class BearerAuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<AllowAnonymousAttribute>().Any();
        if (!requiresAuth)
        {
            return Task.CompletedTask;
        }

        var schemeReference = new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document);
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [schemeReference] = [],
        });

        return Task.CompletedTask;
    }
}
