import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ProviderLinkGroup from './ProviderLinkGroup.tsx';

describe('ProviderLinkGroup', () => {
  it('renders the category heading and each link', () => {
    render(
      <ProviderLinkGroup
        category="Listen"
        links={[
          { platform: 'youtube', url: 'https://www.youtube.com/watch?v=1' },
          {
            platform: 'youtube-music',
            url: 'https://music.youtube.com/watch?v=1',
          },
        ]}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Listen' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'youtube' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=1',
    );
    expect(screen.getByRole('link', { name: 'youtube-music' })).toHaveAttribute(
      'href',
      'https://music.youtube.com/watch?v=1',
    );
  });

  it('opens links in a new tab', () => {
    render(
      <ProviderLinkGroup
        category="Buy"
        links={[{ platform: 'bandcamp', url: 'https://example.bandcamp.com' }]}
      />,
    );

    expect(screen.getByRole('link', { name: 'bandcamp' })).toHaveAttribute(
      'target',
      '_blank',
    );
  });

  it('renders nothing when there are no links', () => {
    const { container } = render(
      <ProviderLinkGroup category="Discover" links={[]} />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
