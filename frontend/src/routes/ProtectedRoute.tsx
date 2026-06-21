import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import type { Role } from '../types/api';

export function ProtectedRoute({ allowedRoles }: { allowedRoles?: Role[] }) {
  const { hasRole, isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return <div className="p-6 text-sm text-slate-500">Ucitavanje profila...</div>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (allowedRoles?.length && !hasRole(...allowedRoles)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
