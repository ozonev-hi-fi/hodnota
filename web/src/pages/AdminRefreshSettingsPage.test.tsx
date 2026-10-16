import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import {
  getRefreshSettings,
  updateRefreshSettings,
} from '../api/adminSettings.ts';
import { ApiError } from '../api/errors.ts';
import AdminRefreshSettingsPage from './AdminRefreshSettingsPage.tsx';

vi.mock('../api/adminSettings.ts', () => ({
  getRefreshSettings: vi.fn(),
  updateRefreshSettings: vi.fn(),
}));

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/admin/refresh-settings']}>
      <Routes>
        <Route
          path="/admin/refresh-settings"
          element={<AdminRefreshSettingsPage />}
        />
        <Route path="/" element={<p>search page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('AdminRefreshSettingsPage', () => {
  it('loads and shows the current settings', async () => {
    vi.mocked(getRefreshSettings).mockResolvedValue({
      notFoundRecheckIntervalDays: 7,
      foundRecheckIntervalDays: 90,
    });

    renderPage();

    expect(await screen.findByDisplayValue('7')).toBeInTheDocument();
    expect(screen.getByDisplayValue('90')).toBeInTheDocument();
  });

  it('limits both inputs to the range the backend accepts', async () => {
    vi.mocked(getRefreshSettings).mockResolvedValue({
      notFoundRecheckIntervalDays: 7,
      foundRecheckIntervalDays: 90,
    });

    renderPage();
    await screen.findByDisplayValue('7');

    for (const input of [
      screen.getByLabelText(/not found/i),
      screen.getByLabelText(/confirmed links/i),
    ]) {
      expect(input).toHaveAttribute('min', '1');
      expect(input).toHaveAttribute('max', '3650');
    }
  });

  it('shows an error when loading the settings fails', async () => {
    vi.mocked(getRefreshSettings).mockRejectedValue(
      new ApiError('Could not load the refresh settings.', 500),
    );

    renderPage();

    expect(
      await screen.findByText('Could not load the refresh settings.'),
    ).toBeInTheDocument();
  });

  it('saves updated settings', async () => {
    vi.mocked(getRefreshSettings).mockResolvedValue({
      notFoundRecheckIntervalDays: 7,
      foundRecheckIntervalDays: 90,
    });
    vi.mocked(updateRefreshSettings).mockResolvedValue({
      notFoundRecheckIntervalDays: 3,
      foundRecheckIntervalDays: 30,
    });

    renderPage();
    await screen.findByDisplayValue('7');

    fireEvent.change(screen.getByLabelText(/not found/i), {
      target: { value: '3' },
    });
    fireEvent.change(screen.getByLabelText(/confirmed links/i), {
      target: { value: '30' },
    });
    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(updateRefreshSettings).toHaveBeenCalledWith(3, 30);
    });
    expect(await screen.findByText('Settings saved.')).toBeInTheDocument();
  });

  it('shows an error when saving fails', async () => {
    vi.mocked(getRefreshSettings).mockResolvedValue({
      notFoundRecheckIntervalDays: 7,
      foundRecheckIntervalDays: 90,
    });
    vi.mocked(updateRefreshSettings).mockRejectedValue(
      new ApiError('Could not save the refresh settings.', 500),
    );

    renderPage();
    await screen.findByDisplayValue('7');

    fireEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(
      await screen.findByText('Could not save the refresh settings.'),
    ).toBeInTheDocument();
  });
});
