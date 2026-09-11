import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function LoginPage() {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
    } catch (err) {
      // ASP.NET Identity returns this exact string for "credentials were correct, but the
      // account isn't allowed to sign in yet" — distinguishable from a plain wrong-password
      // failure, so it's worth a specific, actionable message instead of a generic one.
      if (err instanceof ApiError && err.message === 'NotAllowed') {
        setError(
          'Please confirm your email address before logging in — check your email for the confirmation link.',
        );
      } else {
        setError(userFacingMessage(err, 'Login failed.'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <h1 className="mb-4">Log in</h1>
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
          <label htmlFor="password" className="form-label">
            Password
          </label>
          <input
            id="password"
            type="password"
            className="form-control"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />
        </div>
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? 'Logging in…' : 'Log in'}
        </button>
      </form>
      <p className="mt-3">
        <Link to="/forgot-password">Forgot password?</Link>
      </p>
      <p>
        Don't have an account? <Link to="/register">Register</Link>
      </p>
    </>
  );
}

export default LoginPage;
