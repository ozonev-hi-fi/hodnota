import { type SubmitEvent, useState } from 'react';
import { Link } from 'react-router';
import { userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function RegisterPage() {
  const { register } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [registered, setRegistered] = useState(false);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await register(email, password);
      setRegistered(true);
    } catch (err) {
      setError(userFacingMessage(err, 'Registration failed.'));
    } finally {
      setSubmitting(false);
    }
  }

  if (registered) {
    return (
      <>
        <h1 className="mb-4">Check your email</h1>
        <p>
          We've sent a confirmation link to <strong>{email}</strong>. You'll
          need to confirm your email address before you can{' '}
          <Link to="/login">log in</Link>.
        </p>
      </>
    );
  }

  return (
    <>
      <h1 className="mb-4">Register</h1>
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
          {submitting ? 'Registering…' : 'Register'}
        </button>
      </form>
      <p className="mt-3">
        Already have an account? <Link to="/login">Log in</Link>
      </p>
    </>
  );
}

export default RegisterPage;
