import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import ForgotPasswordPage from './ForgotPasswordPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderPage(forgotPassword: (email: string) => Promise<void>) {
  vi.mocked(useAuth).mockReturnValue({
    forgotPassword,
  } as unknown as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter>
      <ForgotPasswordPage />
    </MemoryRouter>,
  );
}

function fillAndSubmit(email: string) {
  fireEvent.change(screen.getByLabelText('Email'), {
    target: { value: email },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Send reset link' }));
}

describe('ForgotPasswordPage', () => {
  it('calls forgotPassword with the entered email', async () => {
    const forgotPassword = vi.fn().mockResolvedValue(undefined);
    renderPage(forgotPassword);

    fillAndSubmit('a@example.com');

    await vi.waitFor(() =>
      expect(forgotPassword).toHaveBeenCalledWith('a@example.com'),
    );
  });

  it('shows a generic confirmation message after submitting', async () => {
    renderPage(vi.fn().mockResolvedValue(undefined));

    fillAndSubmit('a@example.com');

    expect(await screen.findByText('Check your email')).toBeInTheDocument();
  });

  it('shows an error message when the request fails', async () => {
    renderPage(
      vi.fn().mockRejectedValue(new ApiError('Something went wrong.', 400)),
    );

    fillAndSubmit('a@example.com');

    expect(
      await screen.findByText('Something went wrong.'),
    ).toBeInTheDocument();
  });

  it('links back to login', () => {
    renderPage(vi.fn());

    expect(
      screen.getByRole('link', { name: 'Back to log in' }),
    ).toHaveAttribute('href', '/login');
  });
});
