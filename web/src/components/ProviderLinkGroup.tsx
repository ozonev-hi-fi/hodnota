import type { PlatformRow } from '../api/catalog.ts';
import { platformLabel } from '../api/platforms.ts';

interface ProviderLinkGroupProps {
  category: string;
  rows: PlatformRow[];
}

function ProviderLink({ url }: { url: string }) {
  return (
    <a href={url} target="_blank" rel="noreferrer">
      {url}
    </a>
  );
}

function RowStatus({ row }: { row: PlatformRow }) {
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
      return row.url ? <ProviderLink url={row.url} /> : null;
    case 'OtherVersion':
      return (
        <>
          {row.url && <ProviderLink url={row.url} />}{' '}
          <span className="text-muted small">(other version)</span>
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

function ProviderLinkGroup({ category, rows }: ProviderLinkGroupProps) {
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
            <RowStatus row={row} />
          </li>
        ))}
      </ul>
    </div>
  );
}

export default ProviderLinkGroup;
