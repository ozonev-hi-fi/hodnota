import { apiClient } from './client.ts';
import { unwrap } from './errors.ts';
import type { components } from './generated/openapi-types.ts';

export type SearchCandidate = components['schemas']['SearchCandidateResponse'];
export type SharePage = components['schemas']['SharePageResponse'];
export type SearchType = components['schemas']['CandidateType'];
export type PlatformRow = components['schemas']['PlatformRowResponse'];

interface SharePageWatchHandlers {
  onRow: (row: PlatformRow) => void;
  onComplete: () => void;
  onError: () => void;
}

export async function searchCatalog(
  search: string,
  type: SearchType,
): Promise<SearchCandidate[]> {
  const result = await apiClient.POST('/api/catalog/search', {
    body: { search, type },
  });
  return unwrap(result, 'Search failed.');
}

export async function resolveCatalog(id: string): Promise<SharePage> {
  const result = await apiClient.POST('/api/catalog/resolve', {
    body: { id },
  });
  return unwrap(result, 'Could not create the share page.');
}

export async function getSharePage(id: string): Promise<SharePage> {
  const result = await apiClient.GET('/api/catalog/sharepages/{id}', {
    params: { path: { id } },
  });
  return unwrap(result, 'Could not load the share page.');
}

// openapi-fetch cannot read an event stream, so this uses the browser's EventSource directly.
// It returns a function that closes the stream.
export function watchSharePage(
  id: string,
  handlers: SharePageWatchHandlers,
): () => void {
  const source = new EventSource(
    `/api/catalog/sharepages/${encodeURIComponent(id)}/events`,
  );

  source.addEventListener('platform', (event) => {
    handlers.onRow(JSON.parse((event as MessageEvent<string>).data));
  });
  source.addEventListener('complete', () => {
    source.close();
    handlers.onComplete();
  });
  // Without this close(), EventSource would silently reconnect and replay the stream.
  source.onerror = () => {
    source.close();
    handlers.onError();
  };

  return () => source.close();
}
