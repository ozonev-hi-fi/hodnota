import { Navigate, Route, Routes } from 'react-router';
import RedirectIfAuthenticated from './auth/RedirectIfAuthenticated.tsx';
import RequireAuth from './auth/RequireAuth.tsx';
import CenteredLayout from './layout/CenteredLayout.tsx';
import ChangePasswordPage from './pages/ChangePasswordPage.tsx';
import ConfirmEmailPage from './pages/ConfirmEmailPage.tsx';
import ForgotPasswordPage from './pages/ForgotPasswordPage.tsx';
import LoginPage from './pages/LoginPage.tsx';
import RegisterPage from './pages/RegisterPage.tsx';
import ResetPasswordPage from './pages/ResetPasswordPage.tsx';
import SearchPage from './pages/SearchPage.tsx';
import SharePage from './pages/SharePage.tsx';

function App() {
  return (
    <CenteredLayout>
      <Routes>
        <Route element={<RedirectIfAuthenticated />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        </Route>
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/confirm-email" element={<ConfirmEmailPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/" element={<SearchPage />} />
          <Route
            path="/account/change-password"
            element={<ChangePasswordPage />}
          />
        </Route>
        <Route path="/share/:id" element={<SharePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </CenteredLayout>
  );
}

export default App;
