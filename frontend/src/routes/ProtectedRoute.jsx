import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

const HOME_BY_ROLE = {
  Admin: '/admin/dashboard',
  Dispatcher: '/dispatcher/dashboard',
  Driver: '/driver/dashboard',
}

// Centralizes auth + role gating so no page has to duplicate this logic.
// - Not authenticated  -> /login
// - Authenticated, role allowed -> render the page
// - Authenticated, wrong role   -> that role's own dashboard
export default function ProtectedRoute({ allowedRoles, children }) {
  const { isAuthenticated, role } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (allowedRoles && !allowedRoles.includes(role)) {
    return <Navigate to={HOME_BY_ROLE[role] || '/login'} replace />
  }

  return children
}
