import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { useAuth } from './AuthContext.tsx';
import RedirectIfAuthenticated from './RedirectIfAuthenticated.tsx';

vi.mock('./AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderWithStatus(
  status: 'loading' | 'authenticated' | 'unauthenticated',
) {
  vi.mocked(useAuth).mockReturnValue({ status } as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route element={<RedirectIfAuthenticated />}>
          <Route path="/login" element={<p>login page</p>} />
        </Route>
        <Route path="/" element={<p>home page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('RedirectIfAuthenticated', () => {
  it('renders the page when unauthenticated', () => {
    renderWithStatus('unauthenticated');

    expect(screen.getByText('login page')).toBeInTheDocument();
  });

  it('redirects to / when authenticated', () => {
    renderWithStatus('authenticated');

    expect(screen.getByText('home page')).toBeInTheDocument();
    expect(screen.queryByText('login page')).not.toBeInTheDocument();
  });
});
