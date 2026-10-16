import { apiClient } from './client.ts';
import { unwrap } from './errors.ts';
import type { components } from './generated/openapi-types.ts';

export type RefreshSettings = components['schemas']['RefreshSettingsResponse'];

export async function getRefreshSettings(): Promise<RefreshSettings> {
  const result = await apiClient.GET('/api/admin/refresh-settings');
  return unwrap(result, 'Could not load the refresh settings.');
}

export async function updateRefreshSettings(
  notFoundRecheckIntervalDays: number,
  foundRecheckIntervalDays: number,
): Promise<RefreshSettings> {
  const result = await apiClient.PUT('/api/admin/refresh-settings', {
    body: { notFoundRecheckIntervalDays, foundRecheckIntervalDays },
  });
  return unwrap(result, 'Could not save the refresh settings.');
}

export async function checkAdminAccess(): Promise<boolean> {
  try {
    await getRefreshSettings();
    return true;
  } catch {
    return false;
  }
}
