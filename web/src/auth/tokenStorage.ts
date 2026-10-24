const ACCESS_TOKEN_KEY = 'hodnota.accessToken';
const REFRESH_TOKEN_KEY = 'hodnota.refreshToken';

// api/client.ts clears tokens directly (outside of AuthContext.logout()) when a token refresh
// fails — this lets AuthContext react to that too, so its status doesn't stay stale as
// "authenticated" after a session has actually died.
const clearedListeners = new Set<() => void>();

// A private window, blocked site data, or a browser that disables storage entirely can make any
// localStorage call throw. Falling back to an in-memory map keeps the app usable for that session
// instead of blanking on first render — tokens just don't survive a reload, same as never logging in.
const memoryFallback = new Map<string, string>();

function readToken(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return memoryFallback.get(key) ?? null;
  }
}

function writeToken(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    memoryFallback.set(key, value);
  }
}

function removeToken(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    memoryFallback.delete(key);
  }
}

export function onTokensCleared(listener: () => void): () => void {
  clearedListeners.add(listener);
  return () => clearedListeners.delete(listener);
}

export function getAccessToken(): string | null {
  return readToken(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  return readToken(REFRESH_TOKEN_KEY);
}

export function setTokens(accessToken: string, refreshToken: string): void {
  writeToken(ACCESS_TOKEN_KEY, accessToken);
  writeToken(REFRESH_TOKEN_KEY, refreshToken);
}

export function clearTokens(): void {
  removeToken(ACCESS_TOKEN_KEY);
  removeToken(REFRESH_TOKEN_KEY);
  for (const listener of clearedListeners) {
    listener();
  }
}
