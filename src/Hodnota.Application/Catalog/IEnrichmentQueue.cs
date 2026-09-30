namespace Hodnota.Application.Catalog;

public interface IEnrichmentQueue
{
    // Asks for the given providers to be checked for a share page. A provider already queued or
    // running for that page is not queued twice.
    void Enqueue(Guid sharePageId, IReadOnlyCollection<string> providerCodes);

    // True from the moment a provider is queued for a page until its check has been saved.
    bool IsActive(Guid sharePageId, string providerCode);
}
