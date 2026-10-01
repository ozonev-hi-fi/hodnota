import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router';
import {
  getSharePage,
  type PlatformRow,
  type SharePage as SharePageData,
  watchSharePage,
} from '../api/catalog.ts';
import { userFacingMessage } from '../api/errors.ts';
import type { components } from '../api/generated/openapi-types.ts';
import ProviderLinkGroup from '../components/ProviderLinkGroup.tsx';

type PlatformType = components['schemas']['PlatformType'];
type Category = 'Listen' | 'Buy' | 'Discover';

const CATEGORY_LABELS: Record<PlatformType, Category> = {
  StreamingService: 'Listen',
  DigitalStore: 'Buy',
  PhysicalStore: 'Buy',
  Aggregator: 'Discover',
  Database: 'Discover',
};

const CATEGORY_ORDER: Category[] = ['Listen', 'Buy', 'Discover'];

function SharePage() {
  const { id } = useParams<{ id: string }>();
  const [sharePage, setSharePage] = useState<SharePageData | null>(null);
  const [rows, setRows] = useState<PlatformRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) {
      setError('No share page id was given.');
      setLoading(false);
      return;
    }

    let cancelled = false;
    getSharePage(id)
      .then((page) => {
        if (!cancelled) {
          setSharePage(page);
          setRows(page.platforms);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(userFacingMessage(err, 'Could not load this share page.'));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  // While some platform is still being checked, the server sends each result as it arrives.
  // The stream replays the rows that are already settled first, so a row that changed between
  // the page load and the stream opening is not missed.
  const pageId = sharePage?.id;
  const isComplete = sharePage?.isComplete ?? true;
  useEffect(() => {
    if (!pageId || isComplete) {
      return;
    }

    let stopped = false;
    const stopSpinners = (list: PlatformRow[]): PlatformRow[] =>
      list.map((row) =>
        row.state === 'Checking' ? { ...row, state: 'Failed' } : row,
      );

    const close = watchSharePage(pageId, {
      onRow: (row) =>
        setRows((current) =>
          current.map((existing) =>
            existing.platform === row.platform ? row : existing,
          ),
        ),
      onComplete: () => undefined,
      // The stream ended before every row was settled (a network drop, or the server's time
      // limit). Results may have been saved meanwhile, so reload once; only rows that are still
      // open after that stop spinning.
      onError: () => {
        getSharePage(pageId)
          .then((page) => page.platforms)
          .catch(() => null)
          .then((fresh) => {
            if (!stopped) {
              setRows((current) => stopSpinners(fresh ?? current));
            }
          });
      },
    });

    return () => {
      stopped = true;
      close();
    };
  }, [pageId, isComplete]);

  if (loading) {
    return <h1>Loading…</h1>;
  }

  if (error || !sharePage) {
    return (
      <>
        <h1 className="mb-4">Could not load this page</h1>
        <div className="alert alert-danger">
          {error ?? 'This share page could not be found.'}
        </div>
        <p className="mt-3">
          <Link to="/">Back to search</Link>
        </p>
      </>
    );
  }

  const rowsByCategory: Record<Category, PlatformRow[]> = {
    Listen: [],
    Buy: [],
    Discover: [],
  };
  for (const row of rows) {
    rowsByCategory[CATEGORY_LABELS[row.type]].push(row);
  }

  const linkOf = (platform: string) =>
    rows.find((row) => row.platform === platform && row.state !== 'NotFound')
      ?.url;
  const discogsLink = linkOf('discogs');
  const tidalLink = linkOf('tidal');

  return (
    <>
      <p className="text-muted mb-1">Artist</p>
      <h1 className="mb-3">{sharePage.artist}</h1>
      <p className="text-muted mb-1">{sharePage.type}</p>
      <h2 className="h3 mb-4">{sharePage.name}</h2>
      {CATEGORY_ORDER.map((category) => (
        <ProviderLinkGroup
          key={category}
          category={category}
          rows={rowsByCategory[category]}
        />
      ))}
      {discogsLink && (
        <p className="small text-muted">
          Data provided by{' '}
          <a href={discogsLink} target="_blank" rel="noreferrer">
            Discogs
          </a>
          .
        </p>
      )}
      {tidalLink && (
        <p className="small text-muted">
          Content provided by{' '}
          <a href={tidalLink} target="_blank" rel="noreferrer">
            TIDAL
          </a>
          .
        </p>
      )}
      <p className="mt-3">
        <Link to="/">Back to search</Link>
      </p>
    </>
  );
}

export default SharePage;
