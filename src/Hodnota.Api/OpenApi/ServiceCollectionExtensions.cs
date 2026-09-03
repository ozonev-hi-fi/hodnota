namespace Hodnota.Api.OpenApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options => options
            .AddDocumentTransformer<BearerSecuritySchemeTransformer>()
            .AddOperationTransformer<BearerAuthOperationTransformer>());
}
