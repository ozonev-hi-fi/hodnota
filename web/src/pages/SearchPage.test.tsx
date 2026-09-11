import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { resolveCatalog, searchCatalog } from '../api/catalog.ts';
import { ApiError } from '../api/errors.ts';
import { getAccountInfo } from '../api/identity.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import SearchPage from './SearchPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));
vi.mock('../api/identity.ts', () => ({
  getAccountInfo: vi.fn(),
}));
vi.mock('../api/catalog.ts', () => ({
  searchCatalog: vi.fn(),
  resolveCatalog: vi.fn(),
}));

function renderPage(logout: () => void = vi.fn()) {
  vi.mocked(useAuth).mockReturnValue({ logout } as unknown as ReturnType<
    typeof useAuth
  >);
  vi.mocked(getAccountInfo).mockResolvedValue({
    email: 'someone@example.com',
    isEmailConfirmed: true,
  });

  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<SearchPage />} />
        <Route path="/share/:id" element={<p>Share Page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

function search(text: string) {
  fireEvent.change(screen.getByLabelText('Search'), {
    target: { value: text },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Search' }));
}

describe('SearchPage', () => {
  it('shows the logged-in email once loaded', async () => {
    renderPage();

    expect(
      await screen.findByText('Logged in as someone@example.com'),
    ).toBeInTheDocument();
  });

  it('searches and renders the results', async () => {
    vi.mocked(searchCatalog).mockResolvedValue([
      {
        id: 'c1',
        type: 'Song',
        name: 'Nothing Else Matters',
        artist: 'Metallica',
        imageUrl: null,
      },
    ]);
    renderPage();

    search('nothing');

    expect(await screen.findByText('Nothing Else Matters')).toBeInTheDocument();
    expect(searchCatalog).toHaveBeenCalledWith('nothing');
  });

  it('shows an error message when search fails', async () => {
    vi.mocked(searchCatalog).mockRejectedValue(
      new ApiError('The search provider is currently unavailable.', 400),
    );
    renderPage();

    search('nothing');

    expect(
      await screen.findByText('The search provider is currently unavailable.'),
    ).toBeInTheDocument();
  });

  it('activating a result navigates to its share page', async () => {
    vi.mocked(searchCatalog).mockResolvedValue([
      {
        id: 'c1',
        type: 'Song',
        name: 'Track',
        artist: 'Artist',
        imageUrl: null,
      },
    ]);
    vi.mocked(resolveCatalog).mockResolvedValue({
      id: 'share-1',
      type: 'Song',
      name: 'Track',
      artist: 'Artist',
      links: [],
    });
    renderPage();

    search('track');
    fireEvent.click(await screen.findByText('Track'));

    expect(await screen.findByText('Share Page')).toBeInTheDocument();
    expect(resolveCatalog).toHaveBeenCalledWith('c1');
  });

  it('shows an expired message when the candidate is gone (404)', async () => {
    vi.mocked(searchCatalog).mockResolvedValue([
      {
        id: 'c1',
        type: 'Song',
        name: 'Track',
        artist: 'Artist',
        imageUrl: null,
      },
    ]);
    vi.mocked(resolveCatalog).mockRejectedValue(new ApiError('Not Found', 404));
    renderPage();

    search('track');
    fireEvent.click(await screen.findByText('Track'));

    expect(
      await screen.findByText('This result has expired — please search again.'),
    ).toBeInTheDocument();
  });

  it('calls logout when the Logout button is clicked', async () => {
    const logout = vi.fn();
    renderPage(logout);

    fireEvent.click(await screen.findByRole('button', { name: 'Logout' }));

    expect(logout).toHaveBeenCalled();
  });
});
