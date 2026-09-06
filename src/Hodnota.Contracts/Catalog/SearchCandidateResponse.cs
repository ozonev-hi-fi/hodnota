namespace Hodnota.Contracts.Catalog;

public sealed record SearchCandidateResponse(string Id, CandidateType Type, string Name, string Artist, Uri? ImageUrl);
