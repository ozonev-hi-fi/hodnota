import {
  createContext,
  type ReactNode,
  useContext,
  useEffect,
  useState,
} from 'react';
import { checkAdminAccess } from '../api/adminSettings.ts';
import * as identity from '../api/identity.ts';
import {
  clearTokens,
  getAccessToken,
  onTokensCleared,
} from './tokenStorage.ts';

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated';

interface AuthContextValue {
  status: AuthStatus;
  isAdmin: boolean;
  isAdminLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => void;
  forgotPassword: (email: string) => Promise<void>;
  resetPassword: (
    email: string,
    resetCode: string,
    newPassword: string,
  ) => Promise<void>;
  confirmEmail: (
    userId: string,
    code: string,
    changedEmail?: string,
  ) => Promise<void>;
  changePassword: (oldPassword: string, newPassword: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  // A stored access token means we were logged in before this page load — no network round-trip
  // needed just to answer "are we logged in": a stale/expired token surfaces as a 401 on the
  // first real API call and is handled by api/client.ts's refresh-then-retry logic instead.
  const [status, setStatus] = useState<AuthStatus>(() =>
    getAccessToken() ? 'authenticated' : 'unauthenticated',
  );
  const [isAdmin, setIsAdmin] = useState(false);
  const [isAdminLoading, setIsAdminLoading] = useState(
    status === 'authenticated',
  );

  // api/client.ts clears tokens directly (not through this file) when a token refresh fails —
  // without this, `status` would stay stuck at "authenticated" after a session has actually
  // died, and the route guards would never redirect to /login.
  useEffect(() => onTokensCleared(() => setStatus('unauthenticated')), []);

  // Re-checked on every transition into "authenticated" (page load with a stored token, or a
  // fresh login) — there is no role claim to read locally, so this is the only way to know
  // (see api/adminSettings.ts's checkAdminAccess for why).
  useEffect(() => {
    if (status !== 'authenticated') {
      setIsAdmin(false);
      setIsAdminLoading(false);
      return;
    }

    let cancelled = false;
    setIsAdminLoading(true);
    checkAdminAccess().then((admin) => {
      if (!cancelled) {
        setIsAdmin(admin);
        setIsAdminLoading(false);
      }
    });
    return () => {
      cancelled = true;
    };
  }, [status]);

  async function login(email: string, password: string) {
    await identity.login(email, password);
    setStatus('authenticated');
  }

  function logout() {
    clearTokens();
    setStatus('unauthenticated');
  }

  const value: AuthContextValue = {
    status,
    isAdmin,
    isAdminLoading,
    login,
    register: identity.register,
    logout,
    forgotPassword: identity.forgotPassword,
    resetPassword: identity.resetPassword,
    confirmEmail: identity.confirmEmail,
    changePassword: identity.changePassword,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
