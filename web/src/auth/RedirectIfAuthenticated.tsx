import { Navigate, Outlet } from 'react-router';
import { useAuth } from './AuthContext.tsx';

function RedirectIfAuthenticated() {
  const { status } = useAuth();

  if (status === 'authenticated') {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}

export default RedirectIfAuthenticated;
