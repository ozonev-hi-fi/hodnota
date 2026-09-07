namespace Hodnota.Application.Catalog;

public sealed class SharePageCreationService(ISearchCandidateCache cache, ICatalogRepository repository)
{
    public async Task<SharePageResult?> ResolveAsync(string candidateId, CancellationToken cancellationToken)
    {
        var candidate = cache.Get(candidateId);
        return candidate is null ? null : await repository.CreateSharePageAsync(candidate, cancellationToken);
    }
}
