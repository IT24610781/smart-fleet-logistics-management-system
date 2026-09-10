import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

// Centralizes auth + role gating so no page has to duplicate this logic.
// - Not authenticated  -> /login
// - Authenticated, role allowed -> render the page
// - Authenticated, wrong role   -> /403 (Stitch "403_access_denied_route_guard")
export default function ProtectedRoute({ allowedRoles, children }) {
  const { isAuthenticated, role } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  if (allowedRoles && !allowedRoles.includes(role)) {
    return <Navigate to="/403" replace />
  }

  return children
}
