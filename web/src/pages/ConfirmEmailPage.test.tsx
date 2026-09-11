import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import ConfirmEmailPage from './ConfirmEmailPage.tsx';

vi.mock('../auth/AuthContext.tsx', () => ({
  useAuth: vi.fn(),
}));

function renderAt(
  path: string,
  confirmEmail: (
    userId: string,
    code: string,
    changedEmail?: string,
  ) => Promise<void>,
) {
  vi.mocked(useAuth).mockReturnValue({
    confirmEmail,
  } as unknown as ReturnType<typeof useAuth>);

  return render(
    <MemoryRouter initialEntries={[path]}>
      <ConfirmEmailPage />
    </MemoryRouter>,
  );
}

describe('ConfirmEmailPage', () => {
  it('calls confirmEmail with the userId and code from the URL', async () => {
    const confirmEmail = vi.fn().mockResolvedValue(undefined);
    renderAt('/confirm-email?userId=user-1&code=code-1', confirmEmail);

    await vi.waitFor(() =>
      expect(confirmEmail).toHaveBeenCalledWith('user-1', 'code-1', undefined),
    );
  });

  it('also forwards changedEmail from the URL, for an email-change confirmation link', async () => {
    const confirmEmail = vi.fn().mockResolvedValue(undefined);
    renderAt(
      '/confirm-email?userId=user-1&code=code-1&changedEmail=new@example.com',
      confirmEmail,
    );

    await vi.waitFor(() =>
      expect(confirmEmail).toHaveBeenCalledWith(
        'user-1',
        'code-1',
        'new@example.com',
      ),
    );
  });

  it('shows a success message once confirmed', async () => {
    renderAt(
      '/confirm-email?userId=user-1&code=code-1',
      vi.fn().mockResolvedValue(undefined),
    );

    expect(await screen.findByText('Email confirmed')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'log in' })).toHaveAttribute(
      'href',
      '/login',
    );
  });

  it('shows an error message when confirmation fails', async () => {
    renderAt(
      '/confirm-email?userId=user-1&code=code-1',
      vi.fn().mockRejectedValue(new ApiError('Invalid token.', 400)),
    );

    expect(await screen.findByText('Invalid token.')).toBeInTheDocument();
  });

  it('shows an error without calling confirmEmail when the URL is missing userId/code', async () => {
    const confirmEmail = vi.fn();
    renderAt('/confirm-email', confirmEmail);

    expect(
      await screen.findByText(
        'This confirmation link is missing required information.',
      ),
    ).toBeInTheDocument();
    expect(confirmEmail).not.toHaveBeenCalled();
  });
});
