namespace Hodnota.Application.Catalog;

public interface ISearchCandidateCache
{
    string Store(StreamingSearchResult result);

    StreamingSearchResult? Get(string candidateId);
}
