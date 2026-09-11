import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function ChangePasswordPage() {
  const { changePassword } = useAuth();
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [changed, setChanged] = useState(false);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError('The passwords do not match.');
      return;
    }

    setSubmitting(true);
    try {
      await changePassword(oldPassword, newPassword);
      setChanged(true);
    } catch (err) {
      setError(userFacingMessage(err, 'Could not change the password.'));
    } finally {
      setSubmitting(false);
    }
  }

  if (changed) {
    return (
      <>
        <h1 className="mb-4">Password changed</h1>
        <p>Your password has been updated.</p>
        <p>
          <Link to="/">Back to search</Link>
        </p>
      </>
    );
  }

  return (
    <>
      <h1 className="mb-4">Change password</h1>
      {error && <div className="alert alert-danger">{error}</div>}
      <form onSubmit={handleSubmit}>
        <div className="mb-3">
          <label htmlFor="oldPassword" className="form-label">
            Current password
          </label>
          <input
            id="oldPassword"
            type="password"
            className="form-control"
            value={oldPassword}
            onChange={(event) => setOldPassword(event.target.value)}
            required
          />
        </div>
        <div className="mb-3">
          <label htmlFor="newPassword" className="form-label">
            New password
          </label>
          <input
            id="newPassword"
            type="password"
            className="form-control"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            required
          />
        </div>
        <div className="mb-3">
          <label htmlFor="confirmPassword" className="form-label">
            Confirm new password
          </label>
          <input
            id="confirmPassword"
            type="password"
            className="form-control"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            required
          />
        </div>
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? 'Changing…' : 'Change password'}
        </button>
      </form>
      <p className="mt-3">
        <Link to="/">Back to search</Link>
      </p>
    </>
  );
}

export default ChangePasswordPage;
