import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  clearTokens,
  getAccessToken,
  setTokens,
} from '../auth/tokenStorage.ts';
import { stubFetchWithRelativeUrls } from './relativeUrlFetchStub.ts';

// `apiClient`/`refreshClient` capture `globalThis.fetch` at module-load time (that's how
// openapi-fetch's `createClient` works), so the global has to be stubbed *before* the module is
// evaluated. `vi.resetModules()` + a dynamic import forces a fresh module evaluation against
// whatever `fetch` is currently stubbed, instead of reusing a cached instance from a static
// top-level import.
const fetchMock = vi.fn();

async function loadClient() {
  vi.resetModules();
  return import('./client.ts');
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

beforeEach(() => {
  stubFetchWithRelativeUrls(fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
  clearTokens();
});

describe('apiClient', () => {
  it('attaches the Authorization header from tokenStorage', async () => {
    setTokens('access-1', 'refresh-1');
    fetchMock.mockResolvedValue(
      jsonResponse(200, { email: 'a@example.com', isEmailConfirmed: true }),
    );
    const { apiClient } = await loadClient();

    await apiClient.GET('/api/auth/manage/info');

    const request = fetchMock.mock.calls[0][0] as Request;
    expect(request.headers.get('Authorization')).toBe('Bearer access-1');
  });

  it('refreshes the token and retries once on a 401, then succeeds', async () => {
    setTokens('expired-access', 'refresh-1');
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, {}))
      .mockResolvedValueOnce(
        jsonResponse(200, {
          tokenType: 'Bearer',
          accessToken: 'new-access',
          expiresIn: 3600,
          refreshToken: 'new-refresh',
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse(200, { email: 'a@example.com', isEmailConfirmed: true }),
      );
    const { apiClient } = await loadClient();

    const result = await apiClient.GET('/api/auth/manage/info');

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(result.data).toEqual({
      email: 'a@example.com',
      isEmailConfirmed: true,
    });
    expect(getAccessToken()).toBe('new-access');
  });

  // Regression test: this exact scenario used to deadlock (the refresh call went through the
  // same client, so its own 401 re-entered this handler and awaited the very promise it was
  // part of producing). The timeout race turns a reintroduced deadlock into a clear test
  // failure instead of a hung test run.
  it('does not deadlock when the refresh token itself is invalid, and clears tokens instead', async () => {
    setTokens('expired-access', 'expired-refresh');
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, {}))
      .mockResolvedValueOnce(jsonResponse(401, {}));
    const { apiClient } = await loadClient();

    const result = (await Promise.race([
      apiClient.GET('/api/auth/manage/info'),
      new Promise((_, reject) =>
        setTimeout(() => reject(new Error('timed out — deadlock')), 2000),
      ),
    ])) as Awaited<ReturnType<typeof apiClient.GET>>;

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(result.response.status).toBe(401);
    expect(getAccessToken()).toBeNull();
  });
});
