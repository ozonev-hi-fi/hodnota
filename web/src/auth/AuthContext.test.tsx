import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { AuthProvider, useAuth } from './AuthContext.tsx';
import { clearTokens, setTokens } from './tokenStorage.ts';

function StatusProbe() {
  const { status, logout } = useAuth();
  return (
    <div>
      <span>{status}</span>
      <button type="button" onClick={logout}>
        Logout
      </button>
    </div>
  );
}

describe('AuthContext', () => {
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
});
