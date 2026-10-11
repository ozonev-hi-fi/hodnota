using Hodnota.Domain.Catalog;

namespace Hodnota.Application.Catalog;

public enum PlatformRowState
{
    Checking,
    Found,
    OtherVersion,
    NotFound,
    Failed,
}

// ResultType is set only when it differs from the page's own type — YouTube's verified-track
// fallback for an unverified album (ADR 0018 D2) is the only case today.
public sealed record PlatformRow(string PlatformCode, PlatformType PlatformType, PlatformRowState State, Uri? Url, StreamingResultType? ResultType = null);

// Rows cover every platform of a running provider that supports the item's type, plus any platform
// that already has a stored link. IsComplete: no row is still being checked.
public sealed record SharePageView(SharePageResult Page, IReadOnlyList<PlatformRow> Rows, bool IsComplete);

public abstract record SharePageEvent;

public sealed record PlatformRowEvent(PlatformRow Row) : SharePageEvent;

public sealed record CompleteEvent : SharePageEvent;
