import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App.tsx';
import { AuthProvider } from './auth/AuthContext.tsx';
import { clearTokens, setTokens } from './auth/tokenStorage.ts';

// This suite proves the routing table actually mounts the real pages (not a placeholder) for
// each route — the pages' own API dependencies are mocked here purely so those real pages can
// render without a live backend; each page's own behavior is already covered by its own test
// file (LoginPage.test.tsx, SearchPage.test.tsx, SharePage.test.tsx, etc.).
vi.mock('./api/catalog.ts', () => ({
  searchCatalog: vi.fn(),
  resolveCatalog: vi.fn(),
  getSharePage: vi.fn().mockResolvedValue({
    id: 'share-1',
    type: 'Song',
    name: 'Test Song',
    artist: 'Test Artist',
    links: [],
  }),
}));
vi.mock('./api/identity.ts', () => ({
  register: vi.fn(),
  login: vi.fn(),
  forgotPassword: vi.fn(),
  resetPassword: vi.fn(),
  confirmEmail: vi.fn(),
  getAccountInfo: vi
    .fn()
    .mockResolvedValue({ email: 'test@example.com', isEmailConfirmed: true }),
  changePassword: vi.fn(),
}));

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('App routing', () => {
  afterEach(() => {
    clearTokens();
  });

  it('redirects / to the real login page when unauthenticated', () => {
    renderAt('/');

    expect(screen.getByRole('heading', { name: 'Log in' })).toBeInTheDocument();
  });

  it('renders the real login page directly at /login', () => {
    renderAt('/login');

    expect(screen.getByRole('heading', { name: 'Log in' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('redirects /login to the real search page when already authenticated', () => {
    setTokens('access-1', 'refresh-1');

    renderAt('/login');

    expect(screen.getByRole('heading', { name: 'Search' })).toBeInTheDocument();
  });

  it('renders the real search page directly at / when authenticated', () => {
    setTokens('access-1', 'refresh-1');

    renderAt('/');

    expect(screen.getByRole('heading', { name: 'Search' })).toBeInTheDocument();
    expect(screen.getByLabelText('Search')).toBeInTheDocument();
  });

  it('renders the real, public share page even when unauthenticated', async () => {
    renderAt('/share/share-1');

    expect(await screen.findByText('Test Song')).toBeInTheDocument();
    expect(screen.getByText('Test Artist')).toBeInTheDocument();
  });
});
