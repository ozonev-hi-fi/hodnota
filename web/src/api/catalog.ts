import { apiClient } from './client.ts';
import { unwrap } from './errors.ts';
import type { components } from './generated/openapi-types.ts';

export type SearchCandidate = components['schemas']['SearchCandidateResponse'];
export type SharePage = components['schemas']['SharePageResponse'];

export async function searchCatalog(
  search: string,
): Promise<SearchCandidate[]> {
  const result = await apiClient.POST('/api/catalog/search', {
    body: { search },
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
