import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { checkAdminAccess } from '../api/adminSettings.ts';
import { AuthProvider, useAuth } from './AuthContext.tsx';
import { clearTokens, setTokens } from './tokenStorage.ts';

vi.mock('../api/adminSettings.ts', () => ({
  checkAdminAccess: vi.fn(),
}));

function StatusProbe() {
  const { status, isAdmin, isAdminLoading, logout } = useAuth();
  return (
    <div>
      <span>{status}</span>
      <span>{isAdminLoading ? 'admin-loading' : `isAdmin:${isAdmin}`}</span>
      <button type="button" onClick={logout}>
        Logout
      </button>
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    vi.mocked(checkAdminAccess).mockResolvedValue(false);
  });

  afterEach(() => {
    clearTokens();
  });

  it('starts unauthenticated when no token is stored', () => {
    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );

    expect(screen.getByText('unauthenticated')).toBeInTheDocument();
  });

  it('starts authenticated when a token is already stored', () => {
    setTokens('access-1', 'refresh-1');

    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );

    expect(screen.getByText('authenticated')).toBeInTheDocument();
  });

  it('logout clears the stored tokens and flips status back', () => {
    setTokens('access-1', 'refresh-1');
    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Logout' }));

    expect(screen.getByText('unauthenticated')).toBeInTheDocument();
  });

  it('flips to unauthenticated when tokens are cleared from outside (e.g. a failed token refresh)', () => {
    setTokens('access-1', 'refresh-1');
    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );
    expect(screen.getByText('authenticated')).toBeInTheDocument();

    act(() => {
      clearTokens();
    });

    expect(screen.getByText('unauthenticated')).toBeInTheDocument();
  });

  it('checks admin access when becoming authenticated and exposes the result', async () => {
    vi.mocked(checkAdminAccess).mockResolvedValue(true);
    setTokens('access-1', 'refresh-1');

    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );

    expect(await screen.findByText('isAdmin:true')).toBeInTheDocument();
  });

  it('resets isAdmin to false once unauthenticated', async () => {
    vi.mocked(checkAdminAccess).mockResolvedValue(true);
    setTokens('access-1', 'refresh-1');

    render(
      <AuthProvider>
        <StatusProbe />
      </AuthProvider>,
    );
    await screen.findByText('isAdmin:true');

    fireEvent.click(screen.getByRole('button', { name: 'Logout' }));

    expect(screen.getByText('isAdmin:false')).toBeInTheDocument();
  });
});
