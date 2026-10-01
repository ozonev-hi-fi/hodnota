import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { PlatformRow } from '../api/catalog.ts';
import ProviderLinkGroup from './ProviderLinkGroup.tsx';

function row(overrides: Partial<PlatformRow>): PlatformRow {
  return {
    platform: 'youtube',
    type: 'StreamingService',
    state: 'Found',
    url: 'https://www.youtube.com/watch?v=1',
    ...overrides,
  };
}

function rowOf(label: string): HTMLElement {
  return screen.getByText(label).closest('li') as HTMLElement;
}

describe('ProviderLinkGroup', () => {
  it('renders the category heading and each platform name', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        rows={[row({}), row({ platform: 'youtube-music' })]}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Listen' })).toBeInTheDocument();
    expect(screen.getByText('YouTube')).toBeInTheDocument();
    expect(screen.getByText('YouTube Music')).toBeInTheDocument();
  });

  it('shows a found link as its URL and opens it in a new tab', () => {
    render(<ProviderLinkGroup category="Listen" rows={[row({})]} />);

    const link = within(rowOf('YouTube')).getByRole('link', {
      name: 'https://www.youtube.com/watch?v=1',
    });
    expect(link).toHaveAttribute('href', 'https://www.youtube.com/watch?v=1');
    expect(link).toHaveAttribute('target', '_blank');
    expect(within(rowOf('YouTube')).queryByText(/other version/)).toBeNull();
  });

  it('shows a spinner and no link while a platform is being checked', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        rows={[row({ platform: 'tidal', state: 'Checking', url: null })]}
      />,
    );

    const tidal = rowOf('Tidal');
    expect(within(tidal).getByRole('status')).toHaveTextContent('Checking…');
    expect(within(tidal).queryByRole('link')).toBeNull();
  });

  it('marks a name match as another version', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        rows={[
          row({
            platform: 'tidal',
            state: 'OtherVersion',
            url: 'https://tidal.com/browse/album/9',
          }),
        ]}
      />,
    );

    const tidal = rowOf('Tidal');
    expect(within(tidal).getByRole('link')).toHaveAttribute(
      'href',
      'https://tidal.com/browse/album/9',
    );
    expect(within(tidal).getByText('(other version)')).toBeInTheDocument();
  });

  it('says so when a platform does not have the item', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        rows={[row({ platform: 'qobuz', state: 'NotFound', url: null })]}
      />,
    );

    expect(within(rowOf('Qobuz')).getByText('not found')).toBeInTheDocument();
    expect(within(rowOf('Qobuz')).queryByRole('link')).toBeNull();
  });

  it('says so when a platform could not be checked', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        rows={[row({ platform: 'qobuz', state: 'Failed', url: null })]}
      />,
    );

    expect(
      within(rowOf('Qobuz')).getByText("couldn't check right now"),
    ).toBeInTheDocument();
  });

  it('renders nothing when there are no rows', () => {
    const { container } = render(
      <ProviderLinkGroup category="Discover" rows={[]} />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
