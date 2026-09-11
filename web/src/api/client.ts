import createClient from 'openapi-fetch';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  setTokens,
} from '../auth/tokenStorage.ts';
import type { paths } from './generated/openapi-types.ts';

// Path keys in the generated types already start with "/api", so no baseUrl prefix here — the
// Vite dev proxy (and, later, same-origin production hosting) forward these as-is.
export const apiClient = createClient<paths>();

// A second, middleware-free client for the refresh call itself. Using `apiClient` here would
// deadlock when the refresh token is itself expired/invalid: the refresh request's own 401
// would re-enter this same onResponse handler, which would await the very `refreshPromise` that
// this call is part of producing — a promise that can now never settle.
const refreshClient = createClient<paths>();

// Clones the request while its body is still unread (before it's sent), so it can be safely
// re-sent later even though a Request's body can only be read once.
const requestClones = new WeakMap<Request, Request>();

// These take credentials/a reset code directly, not a bearer token — a 401 (or, for confirmEmail,
// any failure) from one of them means those credentials were wrong, not that the caller's access
// token expired. Refreshing and retrying here would silently log the caller into whatever account
// a still-valid refresh token happens to belong to, rather than surfacing the real error.
const ANONYMOUS_AUTH_PATHS = new Set([
  '/api/auth/login',
  '/api/auth/register',
  '/api/auth/forgotPassword',
  '/api/auth/resetPassword',
  '/api/auth/confirmEmail',
]);

let refreshPromise: Promise<boolean> | null = null;

async function refreshAccessToken(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    return false;
  }

  const { data, error } = await refreshClient.POST('/api/auth/refresh', {
    body: { refreshToken },
  });
  if (error || !data) {
    return false;
  }

  setTokens(data.accessToken, data.refreshToken);
  return true;
}

apiClient.use({
  async onRequest({ request }) {
    const accessToken = getAccessToken();
    if (accessToken) {
      request.headers.set('Authorization', `Bearer ${accessToken}`);
    }
    requestClones.set(request, request.clone());
    return request;
  },
  async onResponse({ request, response }) {
    if (
      response.status !== 401 ||
      ANONYMOUS_AUTH_PATHS.has(new URL(request.url).pathname)
    ) {
      return response;
    }

    refreshPromise ??= refreshAccessToken().finally(() => {
      refreshPromise = null;
    });
    const refreshed = await refreshPromise;

    if (!refreshed) {
      clearTokens();
      return response;
    }

    const retryRequest = requestClones.get(request) ?? request;
    const accessToken = getAccessToken();
    if (accessToken) {
      retryRequest.headers.set('Authorization', `Bearer ${accessToken}`);
    }
    return fetch(retryRequest);
  },
});
