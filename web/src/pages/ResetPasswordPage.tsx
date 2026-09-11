import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function ResetPasswordPage() {
  const { resetPassword } = useAuth();
  const [email, setEmail] = useState('');
  const [resetCode, setResetCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError('The passwords do not match.');
      return;
    }

    setSubmitting(true);
    try {
      await resetPassword(email, resetCode, newPassword);
      setSubmitted(true);
    } catch (err) {
      setError(userFacingMessage(err, 'Could not reset the password.'));
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <>
        <h1 className="mb-4">Password reset</h1>
        <p>
          Your password has been reset. You can now{' '}
          <Link to="/login">log in</Link>.
        </p>
      </>
    );
  }

  return (
    <>
      <h1 className="mb-4">Reset password</h1>
      {error && <div className="alert alert-danger">{error}</div>}
      <form onSubmit={handleSubmit}>
        <div className="mb-3">
          <label htmlFor="email" className="form-label">
            Email
          </label>
          <input
            id="email"
            type="email"
            className="form-control"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />
        </div>
        <div className="mb-3">
          <label htmlFor="resetCode" className="form-label">
            Reset code
          </label>
          <input
            id="resetCode"
            type="text"
            className="form-control"
            value={resetCode}
            onChange={(event) => setResetCode(event.target.value)}
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
          {submitting ? 'Resetting…' : 'Reset password'}
        </button>
      </form>
      <p className="mt-3">
        <Link to="/login">Back to log in</Link>
      </p>
    </>
  );
}

export default ResetPasswordPage;
