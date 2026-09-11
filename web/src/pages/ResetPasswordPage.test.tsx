import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import ResetPasswordPage from './ResetPasswordPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderPage(
  resetPassword: (
    email: string,
    resetCode: string,
    newPassword: string,
  ) => Promise<void>,
) {
  vi.mocked(useAuth).mockReturnValue({
    resetPassword,
  } as unknown as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter>
      <ResetPasswordPage />
    </MemoryRouter>,
  );
}

function fill(
  email: string,
  code: string,
  newPassword: string,
  confirmPassword: string,
) {
  fireEvent.change(screen.getByLabelText('Email'), {
    target: { value: email },
  });
  fireEvent.change(screen.getByLabelText('Reset code'), {
    target: { value: code },
  });
  fireEvent.change(screen.getByLabelText('New password'), {
    target: { value: newPassword },
  });
  fireEvent.change(screen.getByLabelText('Confirm new password'), {
    target: { value: confirmPassword },
  });
}

describe('ResetPasswordPage', () => {
  it('calls resetPassword with the entered email, code, and new password', async () => {
    const resetPassword = vi.fn().mockResolvedValue(undefined);
    renderPage(resetPassword);

    fill('a@example.com', 'the-code', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));

    await vi.waitFor(() =>
      expect(resetPassword).toHaveBeenCalledWith(
        'a@example.com',
        'the-code',
        'NewP@ss1!',
      ),
    );
  });

  it('shows an error and does not call resetPassword when the passwords do not match', () => {
    const resetPassword = vi.fn();
    renderPage(resetPassword);

    fill('a@example.com', 'the-code', 'NewP@ss1!', 'Different1!');
    fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));

    expect(screen.getByText('The passwords do not match.')).toBeInTheDocument();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  it('shows a success message with a login link after resetting', async () => {
    renderPage(vi.fn().mockResolvedValue(undefined));

    fill('a@example.com', 'the-code', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));

    expect(await screen.findByText('Password reset')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'log in' })).toHaveAttribute(
      'href',
      '/login',
    );
  });

  it('shows an error message when the server rejects the code', async () => {
    renderPage(vi.fn().mockRejectedValue(new ApiError('Invalid token.', 400)));

    fill('a@example.com', 'wrong-code', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Reset password' }));

    expect(await screen.findByText('Invalid token.')).toBeInTheDocument();
  });
});
