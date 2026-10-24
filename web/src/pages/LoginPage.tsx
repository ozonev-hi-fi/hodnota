import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { ApiError, userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

// ASP.NET Identity's MapIdentityApi login endpoint returns one of these exact words as the
// ProblemDetails detail on failure (SignInResult.ToString()). Anything else (a future addition,
// e.g. RequiresTwoFactor) falls back to the generic message below rather than showing the raw word.
const LOGIN_FAILURE_MESSAGES: Record<string, string> = {
  NotAllowed:
    'Please confirm your email address before logging in — check your email for the confirmation link.',
  LockedOut: 'Too many failed attempts — your account is temporarily locked. Please try again later.',
  Failed: 'Incorrect email or password.',
};

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
      if (err instanceof ApiError && err.message in LOGIN_FAILURE_MESSAGES) {
        setError(LOGIN_FAILURE_MESSAGES[err.message]);
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
