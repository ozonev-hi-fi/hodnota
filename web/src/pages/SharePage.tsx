import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router';
import {
  getSharePage,
  type SharePage as SharePageData,
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

  const linksByCategory: Record<Category, { platform: string; url: string }[]> =
    {
      Listen: [],
      Buy: [],
      Discover: [],
    };
  for (const link of sharePage.links) {
    linksByCategory[CATEGORY_LABELS[link.type]].push({
      platform: link.platform,
      url: link.url,
    });
  }

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
          links={linksByCategory[category]}
        />
      ))}
      <p className="mt-3">
        <Link to="/">Back to search</Link>
      </p>
    </>
  );
}

export default SharePage;
