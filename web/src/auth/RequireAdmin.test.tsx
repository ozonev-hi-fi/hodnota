import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { useAuth } from './AuthContext.tsx';
import RequireAdmin from './RequireAdmin.tsx';

vi.mock('./AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderWithAdminState(isAdmin: boolean, isAdminLoading: boolean) {
  vi.mocked(useAuth).mockReturnValue({
    isAdmin,
    isAdminLoading,
  } as unknown as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <Routes>
        <Route element={<RequireAdmin />}>
          <Route path="/protected" element={<p>admin content</p>} />
        </Route>
        <Route path="/" element={<p>home page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('RequireAdmin', () => {
  it('renders the admin route when the user is an admin', () => {
    renderWithAdminState(true, false);

    expect(screen.getByText('admin content')).toBeInTheDocument();
  });

  it('redirects to / when the user is not an admin', () => {
    renderWithAdminState(false, false);

    expect(screen.getByText('home page')).toBeInTheDocument();
    expect(screen.queryByText('admin content')).not.toBeInTheDocument();
  });

  it('renders nothing while admin status is loading', () => {
    renderWithAdminState(false, true);

    expect(screen.queryByText('admin content')).not.toBeInTheDocument();
    expect(screen.queryByText('home page')).not.toBeInTheDocument();
  });
});
