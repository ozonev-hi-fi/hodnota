import { act, render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getSharePage,
  type PlatformRow,
  type SharePage as SharePageData,
  watchSharePage,
} from '../api/catalog.ts';
import { ApiError } from '../api/errors.ts';
import SharePage from './SharePage.tsx';

vi.mock('../api/catalog.ts', () => ({
  getSharePage: vi.fn(),
  watchSharePage: vi.fn(),
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

function row(overrides: Partial<PlatformRow>): PlatformRow {
  return {
    platform: 'youtube',
    type: 'StreamingService',
    state: 'Found',
    url: 'https://www.youtube.com/watch?v=1',
    ...overrides,
  };
}

function page(
  platforms: PlatformRow[],
  overrides: Partial<SharePageData> = {},
): SharePageData {
  return {
    id: 'share-1',
    type: 'Song',
    name: 'Nothing Else Matters',
    artist: 'Metallica',
    links: [],
    platforms,
    isComplete: !platforms.some((p) => p.state === 'Checking'),
    ...overrides,
  };
}

type Handlers = Parameters<typeof watchSharePage>[1];

describe('SharePage', () => {
  let handlers: Handlers;
  const closeStream = vi.fn();

  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(watchSharePage).mockImplementation((_id, given) => {
      handlers = given;
      return closeStream;
    });
  });

  it('loads the share page for the id in the URL', async () => {
    vi.mocked(getSharePage).mockResolvedValue(page([]));

    renderAt('share-1');

    expect(await screen.findByText('Nothing Else Matters')).toBeInTheDocument();
    expect(screen.getByText('Metallica')).toBeInTheDocument();
    expect(getSharePage).toHaveBeenCalledWith('share-1');
  });

  it('groups platforms under Listen/Buy/Discover headings and shows their links', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([
        row({}),
        row({
          platform: 'bandcamp',
          type: 'DigitalStore',
          url: 'https://example.bandcamp.com',
        }),
        row({
          platform: 'discogs',
          type: 'Database',
          url: 'https://discogs.com/1',
        }),
      ]),
    );

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.getByRole('heading', { name: 'Listen' })).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'https://www.youtube.com/watch?v=1' }),
    ).toHaveAttribute('href', 'https://www.youtube.com/watch?v=1');
    expect(screen.getByRole('heading', { name: 'Buy' })).toBeInTheDocument();
    expect(screen.getByText('Bandcamp')).toBeInTheDocument();
    const discover = screen
      .getByRole('heading', { name: 'Discover' })
      .closest('div') as HTMLElement;
    expect(
      within(discover).getByRole('link', { name: 'https://discogs.com/1' }),
    ).toHaveAttribute('href', 'https://discogs.com/1');
  });

  it('shows a spinner for a platform that is still being checked and opens the event stream', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([row({ platform: 'tidal', state: 'Checking', url: null })]),
    );

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.getByRole('status')).toHaveTextContent('Checking…');
    expect(watchSharePage).toHaveBeenCalledWith('share-1', expect.any(Object));
  });

  it('replaces the spinner with the link when the result arrives', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([
        row({ platform: 'tidal', state: 'Checking', url: null }),
        row({ platform: 'qobuz', state: 'Checking', url: null }),
      ]),
    );
    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    act(() =>
      handlers.onRow(
        row({
          platform: 'tidal',
          state: 'Found',
          url: 'https://tidal.com/browse/track/1',
        }),
      ),
    );

    expect(
      screen.getByRole('link', { name: 'https://tidal.com/browse/track/1' }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole('status')).toHaveLength(1);
  });

  it('reloads the page when the stream ends early and stops only the spinners that are still open', async () => {
    vi.mocked(getSharePage)
      .mockResolvedValueOnce(
        page([
          row({ platform: 'tidal', state: 'Checking', url: null }),
          row({ platform: 'qobuz', state: 'Checking', url: null }),
        ]),
      )
      .mockResolvedValueOnce(
        page([
          row({
            platform: 'tidal',
            state: 'Found',
            url: 'https://tidal.com/browse/track/1',
          }),
          row({ platform: 'qobuz', state: 'Checking', url: null }),
        ]),
      );
    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    act(() => handlers.onError());

    expect(
      await screen.findByRole('link', {
        name: 'https://tidal.com/browse/track/1',
      }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('status')).toBeNull();
    expect(screen.getByText("couldn't check right now")).toBeInTheDocument();
  });

  it('stops the spinners when the stream fails and the reload fails too', async () => {
    vi.mocked(getSharePage)
      .mockResolvedValueOnce(
        page([row({ platform: 'tidal', state: 'Checking', url: null })]),
      )
      .mockRejectedValueOnce(new ApiError('offline', 500));
    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    act(() => handlers.onError());

    expect(
      await screen.findByText("couldn't check right now"),
    ).toBeInTheDocument();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('closes the event stream when the page is left', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([row({ platform: 'tidal', state: 'Checking', url: null })]),
    );
    const { unmount } = renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    unmount();

    expect(closeStream).toHaveBeenCalled();
  });

  it('does not open an event stream when every platform is already settled', async () => {
    vi.mocked(getSharePage).mockResolvedValue(page([row({})]));

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(watchSharePage).not.toHaveBeenCalled();
  });

  it('credits Discogs and links back to the specific page when a Discogs link is present', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page(
        [
          row({
            platform: 'discogs',
            type: 'Database',
            url: 'https://www.discogs.com/release/1',
          }),
        ],
        { type: 'Album' },
      ),
    );

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    const credit = screen.getByText(/Data provided by/);
    expect(
      within(credit).getByRole('link', { name: 'Discogs' }),
    ).toHaveAttribute('href', 'https://www.discogs.com/release/1');
  });

  it('does not show a Discogs credit when Discogs has no link', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([
        row({
          platform: 'discogs',
          type: 'Database',
          state: 'NotFound',
          url: null,
        }),
        row({}),
      ]),
    );

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    expect(screen.queryByText(/Data provided by/)).not.toBeInTheDocument();
  });

  it('credits TIDAL and links back to the specific page when a Tidal link is present', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page(
        [
          row({
            platform: 'tidal',
            url: 'https://tidal.com/browse/album/1',
          }),
        ],
        { type: 'Album' },
      ),
    );

    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');

    const credit = screen.getByText(/Content provided by/);
    expect(within(credit).getByRole('link', { name: 'TIDAL' })).toHaveAttribute(
      'href',
      'https://tidal.com/browse/album/1',
    );
  });

  it('shows the TIDAL credit as soon as the Tidal link arrives', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([row({ platform: 'tidal', state: 'Checking', url: null })]),
    );
    renderAt('share-1');
    await screen.findByText('Nothing Else Matters');
    expect(screen.queryByText(/Content provided by/)).not.toBeInTheDocument();

    act(() =>
      handlers.onRow(
        row({ platform: 'tidal', url: 'https://tidal.com/browse/track/1' }),
      ),
    );

    expect(screen.getByText(/Content provided by/)).toBeInTheDocument();
  });

  it('does not show a TIDAL credit when Tidal has no link', async () => {
    vi.mocked(getSharePage).mockResolvedValue(
      page([row({ platform: 'tidal', state: 'NotFound', url: null }), row({})]),
    );

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
