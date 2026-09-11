const ACCESS_TOKEN_KEY = 'hodnota.accessToken';
const REFRESH_TOKEN_KEY = 'hodnota.refreshToken';

// api/client.ts clears tokens directly (outside of AuthContext.logout()) when a token refresh
// fails — this lets AuthContext react to that too, so its status doesn't stay stale as
// "authenticated" after a session has actually died.
const clearedListeners = new Set<() => void>();

export function onTokensCleared(listener: () => void): () => void {
  clearedListeners.add(listener);
  return () => clearedListeners.delete(listener);
}

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setTokens(accessToken: string, refreshToken: string): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  for (const listener of clearedListeners) {
    listener();
  }
}
