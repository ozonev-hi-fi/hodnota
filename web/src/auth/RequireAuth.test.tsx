import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { useAuth } from './AuthContext.tsx';
import RequireAuth from './RequireAuth.tsx';

vi.mock('./AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderWithStatus(
  status: 'loading' | 'authenticated' | 'unauthenticated',
) {
  vi.mocked(useAuth).mockReturnValue({ status } as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <Routes>
        <Route element={<RequireAuth />}>
          <Route path="/protected" element={<p>secret content</p>} />
        </Route>
        <Route path="/login" element={<p>login page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('RequireAuth', () => {
  it('renders the protected route when authenticated', () => {
    renderWithStatus('authenticated');

    expect(screen.getByText('secret content')).toBeInTheDocument();
  });

  it('redirects to /login when unauthenticated', () => {
    renderWithStatus('unauthenticated');

    expect(screen.getByText('login page')).toBeInTheDocument();
    expect(screen.queryByText('secret content')).not.toBeInTheDocument();
  });

  it('renders nothing while loading', () => {
    renderWithStatus('loading');

    expect(screen.queryByText('secret content')).not.toBeInTheDocument();
    expect(screen.queryByText('login page')).not.toBeInTheDocument();
  });
});
