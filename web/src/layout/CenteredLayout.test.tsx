import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import CenteredLayout from './CenteredLayout.tsx';

describe('CenteredLayout', () => {
  it('renders its children', () => {
    render(
      <CenteredLayout>
        <p>content</p>
      </CenteredLayout>,
    );

    expect(screen.getByText('content')).toBeInTheDocument();
  });
});
