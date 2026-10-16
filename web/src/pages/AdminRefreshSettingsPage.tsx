import { type SubmitEvent, useEffect, useState } from 'react';
import { Link } from 'react-router';
import {
  getRefreshSettings,
  updateRefreshSettings,
} from '../api/adminSettings.ts';
import { userFacingMessage } from '../api/errors.ts';

// Matches UpdateRefreshSettingsRequest.MaxIntervalDays on the backend, which rejects anything above it.
const MAX_INTERVAL_DAYS = 3650;

function AdminRefreshSettingsPage() {
  const [notFoundDays, setNotFoundDays] = useState('');
  const [foundDays, setFoundDays] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getRefreshSettings()
      .then((settings) => {
        if (cancelled) {
          return;
        }
        setNotFoundDays(String(settings.notFoundRecheckIntervalDays));
        setFoundDays(String(settings.foundRecheckIntervalDays));
      })
      .catch((err) => {
        if (!cancelled) {
          setError(
            userFacingMessage(err, 'Could not load the refresh settings.'),
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSaved(false);
    setSaving(true);
    try {
      const settings = await updateRefreshSettings(
        Number(notFoundDays),
        Number(foundDays),
      );
      setNotFoundDays(String(settings.notFoundRecheckIntervalDays));
      setFoundDays(String(settings.foundRecheckIntervalDays));
      setSaved(true);
    } catch (err) {
      setError(userFacingMessage(err, 'Could not save the refresh settings.'));
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <p>Loading…</p>;
  }

  return (
    <>
      <h1 className="mb-4">Scheduled refresh settings</h1>
      {error && <div className="alert alert-danger">{error}</div>}
      {saved && <div className="alert alert-success">Settings saved.</div>}
      <form onSubmit={handleSubmit}>
        <div className="mb-3">
          <label htmlFor="notFoundDays" className="form-label">
            Re-check "not found" platforms every (days)
          </label>
          <input
            id="notFoundDays"
            type="number"
            min={1}
            max={MAX_INTERVAL_DAYS}
            className="form-control"
            value={notFoundDays}
            onChange={(event) => setNotFoundDays(event.target.value)}
            required
          />
        </div>
        <div className="mb-3">
          <label htmlFor="foundDays" className="form-label">
            Re-check confirmed links every (days)
          </label>
          <input
            id="foundDays"
            type="number"
            min={1}
            max={MAX_INTERVAL_DAYS}
            className="form-control"
            value={foundDays}
            onChange={(event) => setFoundDays(event.target.value)}
            required
          />
        </div>
        <button type="submit" className="btn btn-primary" disabled={saving}>
          {saving ? 'Saving…' : 'Save'}
        </button>
      </form>
      <p className="mt-3">
        <Link to="/">Back to search</Link>
      </p>
    </>
  );
}

export default AdminRefreshSettingsPage;
