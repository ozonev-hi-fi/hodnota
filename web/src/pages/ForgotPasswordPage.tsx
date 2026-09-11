import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function ForgotPasswordPage() {
  const { forgotPassword } = useAuth();
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await forgotPassword(email);
      setSubmitted(true);
    } catch (err) {
      setError(userFacingMessage(err, 'Could not send a password reset link.'));
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <>
        <h1 className="mb-4">Check your email</h1>
        <p>
          If an account exists for that email, a password reset link has been
          sent.
        </p>
        <p>
          <Link to="/login">Back to log in</Link>
        </p>
      </>
    );
  }

  return (
    <>
      <h1 className="mb-4">Forgot password</h1>
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
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? 'Sending…' : 'Send reset link'}
        </button>
      </form>
      <p className="mt-3">
        <Link to="/login">Back to log in</Link>
      </p>
    </>
  );
}

export default ForgotPasswordPage;
