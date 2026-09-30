import { render, screen, within } from '@testing-library/react';
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
    expect(screen.getByRole('link', { name: 'YouTube' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=1',
    );
    expect(screen.getByRole('heading', { name: 'Buy' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Bandcamp' })).toBeInTheDocument();
    const discover = screen
      .getByRole('heading', { name: 'Discover' })
      .closest('div') as HTMLElement;
    expect(
      within(discover).getByRole('link', { name: 'Discogs' }),
    ).toHaveAttribute('href', 'https://discogs.com/1');
  });

  it('credits Discogs and links back to the specific page when a Discogs link is present', async () => {
    vi.mocked(getSharePage).mockResolvedValue({
      id: 'share-1',
      type: 'Album',
      name: 'Nothing Else Matters',
      artist: 'Metallica',
      links: [
        {
          platform: 'discogs',
          url: 'https://www.discogs.com/release/1',
          type: 'Database',
        },
      ],
    });

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    const credit = screen.getByText(/Data provided by/);
    expect(
      within(credit).getByRole('link', { name: 'Discogs' }),
    ).toHaveAttribute('href', 'https://www.discogs.com/release/1');
  });

  it('does not show a Discogs credit when there is no Discogs link', async () => {
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
      ],
    });

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.queryByText(/Data provided by/)).not.toBeInTheDocument();
  });

  it('credits TIDAL and links back to the specific page when a Tidal link is present', async () => {
    vi.mocked(getSharePage).mockResolvedValue({
      id: 'share-1',
      type: 'Album',
      name: 'Nothing Else Matters',
      artist: 'Metallica',
      links: [
        {
          platform: 'tidal',
          url: 'https://tidal.com/browse/album/1',
          type: 'StreamingService',
        },
      ],
    });

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    const credit = screen.getByText(/Content provided by/);
    expect(within(credit).getByRole('link', { name: 'TIDAL' })).toHaveAttribute(
      'href',
      'https://tidal.com/browse/album/1',
    );
  });

  it('does not show a TIDAL credit when there is no Tidal link', async () => {
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
      ],
    });

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.queryByText(/Content provided by/)).not.toBeInTheDocument();
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
