import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { getSharePage } from '../api/catalog.ts';
import { ApiError } from '../api/errors.ts';
import SharePage from './SharePage.tsx';

vi.mock('../api/catalog.ts', () => ({
  getSharePage: vi.fn(),
}));

function renderAt(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/share/${id}`]}>
      <Routes>
        <Route path="/share/:id" element={<SharePage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('SharePage', () => {
  it('loads the share page for the id in the URL', async () => {
    vi.mocked(getSharePage).mockResolvedValue({
      id: 'share-1',
      type: 'Song',
      name: 'Nothing Else Matters',
      artist: 'Metallica',
      links: [],
    });

    renderAt('share-1');

    expect(await screen.findByText('Nothing Else Matters')).toBeInTheDocument();
    expect(screen.getByText('Metallica')).toBeInTheDocument();
    expect(getSharePage).toHaveBeenCalledWith('share-1');
  });

  it('groups links under Listen/Buy/Discover headings', async () => {
    vi.mocked(getSharePage).mockResolvedValue({
      id: 'share-1',
      type: 'Song',
      name: 'Nothing Else Matters',
      artist: 'Metallica',
      links: [
        {
          platform: 'youtube',
          url: 'https://www.youtube.com/watch?v=1',
          type: 'StreamingService',
        },
        {
          platform: 'bandcamp',
          url: 'https://example.bandcamp.com',
          type: 'DigitalStore',
        },
        { platform: 'discogs', url: 'https://discogs.com/1', type: 'Database' },
      ],
    });

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.getByRole('heading', { name: 'Listen' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'youtube' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=1',
    );
    expect(screen.getByRole('heading', { name: 'Buy' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'bandcamp' })).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Discover' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'discogs' })).toBeInTheDocument();
  });

  it('shows an error message when loading fails', async () => {
    vi.mocked(getSharePage).mockRejectedValue(
      new ApiError('Could not load the share page.', 404),
    );

    renderAt('missing-id');

    expect(
      await screen.findByText('Could not load the share page.'),
    ).toBeInTheDocument();
  });
});
