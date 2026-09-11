import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import RegisterPage from './RegisterPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderPage(
  register: (email: string, password: string) => Promise<void>,
) {
  vi.mocked(useAuth).mockReturnValue({ register } as unknown as ReturnType<
    typeof useAuth
  >);

  return render(
    <MemoryRouter>
      <RegisterPage />
    </MemoryRouter>,
  );
}

function fillAndSubmit(email: string, password: string) {
  fireEvent.change(screen.getByLabelText('Email'), {
    target: { value: email },
  });
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: password },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Register' }));
}

describe('RegisterPage', () => {
  it('calls register with the entered email and password', async () => {
    const register = vi.fn().mockResolvedValue(undefined);
    renderPage(register);

    fillAndSubmit('a@example.com', 'secret123');

    await vi.waitFor(() =>
      expect(register).toHaveBeenCalledWith('a@example.com', 'secret123'),
    );
  });

  it('shows a check-your-email message with a login link after registering', async () => {
    renderPage(vi.fn().mockResolvedValue(undefined));

    fillAndSubmit('a@example.com', 'secret123');

    expect(await screen.findByText('Check your email')).toBeInTheDocument();
    expect(screen.getByText('a@example.com')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'log in' })).toHaveAttribute(
      'href',
      '/login',
    );
  });

  it('shows an error message when registration fails', async () => {
    renderPage(
      vi.fn().mockRejectedValue(new ApiError('Email already registered.', 400)),
    );

    fillAndSubmit('a@example.com', 'secret123');

    expect(
      await screen.findByText('Email already registered.'),
    ).toBeInTheDocument();
  });

  it('links to login', () => {
    renderPage(vi.fn());

    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute(
      'href',
      '/login',
    );
  });
});
