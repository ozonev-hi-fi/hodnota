import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import LoginPage from './LoginPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderPage(login: (email: string, password: string) => Promise<void>) {
  vi.mocked(useAuth).mockReturnValue({ login } as unknown as ReturnType<
    typeof useAuth
  >);

  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  it('calls login with the entered email and password', async () => {
    const login = vi.fn().mockResolvedValue(undefined);
    renderPage(login);

    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'a@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'secret123' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Log in' }));

    await vi.waitFor(() =>
      expect(login).toHaveBeenCalledWith('a@example.com', 'secret123'),
    );
  });

  it('shows an error message when login fails', async () => {
    const login = vi
      .fn()
      .mockRejectedValue(new ApiError('Invalid credentials.', 401));
    renderPage(login);

    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'a@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'wrong' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByText('Invalid credentials.')).toBeInTheDocument();
  });

  // Regression test: a raw, uncurated exception (a network failure, a parsing bug, anything
  // that isn't our own ApiError) must never be shown to the user verbatim — this exact class of
  // bug once leaked a raw "Unexpected token ... is not valid JSON" parser error onto a page.
  it('shows a generic message, not the raw error, when something unexpected throws', async () => {
    const login = vi
      .fn()
      .mockRejectedValue(
        new SyntaxError(
          'Unexpected token \'T\', "Thank you "... is not valid JSON',
        ),
      );
    renderPage(login);

    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'a@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'secret123' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByText('Login failed.')).toBeInTheDocument();
    expect(screen.queryByText(/Unexpected token/)).not.toBeInTheDocument();
  });

  it('shows a specific message when the account email is not confirmed', async () => {
    const login = vi.fn().mockRejectedValue(new ApiError('NotAllowed', 401));
    renderPage(login);

    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'a@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'secret123' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Log in' }));

    expect(
      await screen.findByText(
        'Please confirm your email address before logging in — check your email for the confirmation link.',
      ),
    ).toBeInTheDocument();
  });

  it('links to register and forgot-password', () => {
    renderPage(vi.fn());

    expect(screen.getByRole('link', { name: 'Register' })).toHaveAttribute(
      'href',
      '/register',
    );
    expect(
      screen.getByRole('link', { name: 'Forgot password?' }),
    ).toHaveAttribute('href', '/forgot-password');
  });
});
