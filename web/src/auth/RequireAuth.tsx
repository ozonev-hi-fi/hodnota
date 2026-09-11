import { Navigate, Outlet } from 'react-router';
import { useAuth } from './AuthContext.tsx';

function RequireAuth() {
  const { status } = useAuth();

  if (status === 'loading') {
    return null;
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}

export default RequireAuth;
