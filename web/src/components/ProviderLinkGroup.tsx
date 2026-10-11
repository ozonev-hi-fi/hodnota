import type { PlatformRow } from '../api/catalog.ts';
import { platformLabel } from '../api/platforms.ts';
import type { components } from '../api/generated/openapi-types.ts';

type CandidateType = components['schemas']['CandidateType'];

interface ProviderLinkGroupProps {
  category: string;
  rows: PlatformRow[];
  pageType: CandidateType;
}

function ProviderLink({ url }: { url: string }) {
  return (
    <a href={url} target="_blank" rel="noreferrer">
      {url}
    </a>
  );
}

// A link whose resultType differs from the page's own type is YouTube's verified-track fallback
// for an unverified album (ADR 0018 D2) — "labelled as a track, never the full album".
function TrackLabel({ row, pageType }: { row: PlatformRow; pageType: CandidateType }) {
  if (!row.resultType || row.resultType === pageType) {
    return null;
  }

  return <span className="text-muted small"> (track)</span>;
}

function RowStatus({ row, pageType }: { row: PlatformRow; pageType: CandidateType }) {
  switch (row.state) {
    case 'Checking':
      return (
        <span
          className="spinner-border spinner-border-sm text-secondary"
          role="status"
        >
          <span className="visually-hidden">Checking…</span>
        </span>
      );
    case 'Found':
      return row.url ? (
        <>
          <ProviderLink url={row.url} />
          <TrackLabel row={row} pageType={pageType} />
        </>
      ) : null;
    case 'OtherVersion':
      return (
        <>
          {row.url && <ProviderLink url={row.url} />}{' '}
          <span className="text-muted small">(other version)</span>
          <TrackLabel row={row} pageType={pageType} />
        </>
      );
    case 'NotFound':
      return <span className="text-muted">not found</span>;
    case 'Failed':
      return (
        <>
          <span className="text-muted">couldn't check right now</span>{' '}
          {row.url && <ProviderLink url={row.url} />}
        </>
      );
  }
}

function ProviderLinkGroup({ category, rows, pageType }: ProviderLinkGroupProps) {
  if (rows.length === 0) {
    return null;
  }

  return (
    <div className="mb-4">
      <h2 className="h5">{category}</h2>
      <ul className="list-unstyled">
        {rows.map((row) => (
          <li key={row.platform} className="mb-2">
            <span className="me-2">{platformLabel(row.platform)}</span>
            <RowStatus row={row} pageType={pageType} />
          </li>
        ))}
      </ul>
    </div>
  );
}

export default ProviderLinkGroup;
