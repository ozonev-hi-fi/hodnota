import { Navigate, Outlet } from 'react-router';
import { useAuth } from './AuthContext.tsx';

// Nested inside RequireAuth's route, so a non-authenticated viewer never reaches here at all —
// this only decides authenticated-but-not-admin vs admin.
function RequireAdmin() {
  const { isAdmin, isAdminLoading } = useAuth();

  if (isAdminLoading) {
    return null;
  }

  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}

export default RequireAdmin;
