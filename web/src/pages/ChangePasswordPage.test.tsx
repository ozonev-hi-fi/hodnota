import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import ChangePasswordPage from './ChangePasswordPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderPage(
  changePassword: (oldPassword: string, newPassword: string) => Promise<void>,
) {
  vi.mocked(useAuth).mockReturnValue({
    changePassword,
  } as unknown as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter>
      <ChangePasswordPage />
    </MemoryRouter>,
  );
}

function fill(
  oldPassword: string,
  newPassword: string,
  confirmPassword: string,
) {
  fireEvent.change(screen.getByLabelText('Current password'), {
    target: { value: oldPassword },
  });
  fireEvent.change(screen.getByLabelText('New password'), {
    target: { value: newPassword },
  });
  fireEvent.change(screen.getByLabelText('Confirm new password'), {
    target: { value: confirmPassword },
  });
}

describe('ChangePasswordPage', () => {
  it('calls changePassword with the entered old and new passwords', async () => {
    const changePassword = vi.fn().mockResolvedValue(undefined);
    renderPage(changePassword);

    fill('OldP@ss1!', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));

    await vi.waitFor(() =>
      expect(changePassword).toHaveBeenCalledWith('OldP@ss1!', 'NewP@ss1!'),
    );
  });

  it('shows an error and does not call changePassword when the new passwords do not match', () => {
    const changePassword = vi.fn();
    renderPage(changePassword);

    fill('OldP@ss1!', 'NewP@ss1!', 'Different1!');
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));

    expect(screen.getByText('The passwords do not match.')).toBeInTheDocument();
    expect(changePassword).not.toHaveBeenCalled();
  });

  it('shows a success view with a link back to search', async () => {
    renderPage(vi.fn().mockResolvedValue(undefined));

    fill('OldP@ss1!', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText('Password changed')).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Back to search' }),
    ).toHaveAttribute('href', '/');
  });

  it('shows an error message when the server rejects the request', async () => {
    renderPage(
      vi.fn().mockRejectedValue(new ApiError('Incorrect password.', 400)),
    );

    fill('WrongOld1!', 'NewP@ss1!', 'NewP@ss1!');
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));

    expect(await screen.findByText('Incorrect password.')).toBeInTheDocument();
  });
});
