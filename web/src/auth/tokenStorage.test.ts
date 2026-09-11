import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  onTokensCleared,
  setTokens,
} from './tokenStorage.ts';

describe('tokenStorage', () => {
  afterEach(() => {
    clearTokens();
  });

  it('returns null for both tokens when nothing was stored', () => {
    expect(getAccessToken()).toBeNull();
    expect(getRefreshToken()).toBeNull();
  });

  it('returns what was stored by setTokens', () => {
    setTokens('access-1', 'refresh-1');

    expect(getAccessToken()).toBe('access-1');
    expect(getRefreshToken()).toBe('refresh-1');
  });

  it('clears both tokens', () => {
    setTokens('access-1', 'refresh-1');

    clearTokens();

    expect(getAccessToken()).toBeNull();
    expect(getRefreshToken()).toBeNull();
  });

  it('notifies subscribers when tokens are cleared', () => {
    const listener = vi.fn();
    onTokensCleared(listener);

    clearTokens();

    expect(listener).toHaveBeenCalledOnce();
  });

  it('stops notifying a subscriber once it unsubscribes', () => {
    const listener = vi.fn();
    const unsubscribe = onTokensCleared(listener);
    unsubscribe();

    clearTokens();

    expect(listener).not.toHaveBeenCalled();
  });
});
