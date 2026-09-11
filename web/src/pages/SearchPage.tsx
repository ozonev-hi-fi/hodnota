import { type SubmitEvent, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import {
  resolveCatalog,
  type SearchCandidate,
  searchCatalog,
} from '../api/catalog.ts';
import { ApiError, userFacingMessage } from '../api/errors.ts';
import { getAccountInfo } from '../api/identity.ts';
import { useAuth } from '../auth/AuthContext.tsx';
import KeyboardNavigableList from '../components/KeyboardNavigableList.tsx';

function SearchPage() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchCandidate[]>([]);
  const [searchToken, setSearchToken] = useState(0);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAccountInfo()
      .then((info) => setEmail(info.email))
      .catch(() => {
        // Not critical to the page working — just skip showing the email if it can't be loaded.
      });
  }, []);

  async function handleSearch(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSearching(true);
    try {
      const candidates = await searchCatalog(query);
      setResults(candidates);
      setSearchToken((token) => token + 1);
    } catch (err) {
      setResults([]);
      setError(userFacingMessage(err, 'Search failed.'));
    } finally {
      setSearching(false);
    }
  }

  async function handleActivate(candidate: SearchCandidate) {
    setError(null);
    try {
      const sharePage = await resolveCatalog(candidate.id);
      navigate(`/share/${sharePage.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        setError('This result has expired — please search again.');
        setResults([]);
      } else {
        setError(userFacingMessage(err, 'Could not open this result.'));
      }
    }
  }

  return (
    <>
      <h1 className="mb-4">Search</h1>
      <p className="text-muted small">
        <span>{email ? `Logged in as ${email}` : 'Logged in'}</span> ·{' '}
        <Link to="/account/change-password">Change password</Link> ·{' '}
        <button
          type="button"
          className="btn btn-link btn-sm p-0 align-baseline"
          onClick={logout}
        >
          Logout
        </button>
      </p>
      {error && <div className="alert alert-danger">{error}</div>}
      <form onSubmit={handleSearch} className="d-flex gap-2 mb-4">
        <input
          type="text"
          className="form-control"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search for a song or album"
          aria-label="Search"
          required
        />
        <button type="submit" className="btn btn-primary" disabled={searching}>
          {searching ? 'Searching…' : 'Search'}
        </button>
      </form>
      {results.length > 0 && (
        <KeyboardNavigableList
          key={searchToken}
          items={results}
          getKey={(candidate) => candidate.id}
          renderItem={(candidate) => (
            <>
              <strong>{candidate.name}</strong> — {candidate.artist}{' '}
              <span className="text-muted">({candidate.type})</span>
            </>
          )}
          onActivate={handleActivate}
        />
      )}
    </>
  );
}

export default SearchPage;
