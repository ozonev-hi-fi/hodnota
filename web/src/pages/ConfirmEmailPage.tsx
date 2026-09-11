import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { userFacingMessage } from '../api/errors.ts';
import { useAuth } from '../auth/AuthContext.tsx';

function ConfirmEmailPage() {
  const { confirmEmail } = useAuth();
  const [searchParams] = useSearchParams();
  const userId = searchParams.get('userId');
  const code = searchParams.get('code');
  const changedEmail = searchParams.get('changedEmail') ?? undefined;
  const [status, setStatus] = useState<'confirming' | 'success' | 'error'>(
    'confirming',
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!userId || !code) {
      setStatus('error');
      setError('This confirmation link is missing required information.');
      return;
    }

    let cancelled = false;
    confirmEmail(userId, code, changedEmail)
      .then(() => {
        if (!cancelled) {
          setStatus('success');
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setStatus('error');
          setError(
            userFacingMessage(err, 'Could not confirm the email address.'),
          );
        }
      });

    return () => {
      cancelled = true;
    };
  }, [userId, code, changedEmail, confirmEmail]);

  if (status === 'confirming') {
    return <h1>Confirming your email…</h1>;
  }

  if (status === 'success') {
    return (
      <>
        <h1 className="mb-4">Email confirmed</h1>
        <p>
          You can now <Link to="/login">log in</Link>.
        </p>
      </>
    );
  }

  return (
    <>
      <h1 className="mb-4">Could not confirm email</h1>
      <div className="alert alert-danger">{error}</div>
      <p>
        <Link to="/login">Back to log in</Link>
      </p>
    </>
  );
}

export default ConfirmEmailPage;
