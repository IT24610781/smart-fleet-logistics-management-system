import { Link, useNavigate } from 'react-router-dom'
import Button from '../../components/common/Button'
import { useAuth } from '../../context/AuthContext'

const HOME_BY_ROLE = {
  Admin: '/admin/dashboard',
  Dispatcher: '/dispatcher/dashboard',
  Driver: '/driver/dashboard',
}

// Follows the Stitch "403_access_denied_route_guard" screen. Reached when
// ProtectedRoute finds an authenticated user whose role isn't allowed on
// the requested route.
export default function Forbidden() {
  const { role, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const home = isAuthenticated ? HOME_BY_ROLE[role] || '/login' : '/login'

  return (
    <div className="min-h-screen flex items-center justify-center bg-surface p-4">
      <div className="w-full max-w-md text-center rounded-xl border border-outline-variant bg-surface-container-lowest p-8">
        <div className="mx-auto w-16 h-16 rounded-full bg-error-container flex items-center justify-center text-on-error-container mb-5">
          <span className="material-symbols-outlined text-3xl">block</span>
        </div>
        <h1 className="font-headline-lg text-headline-lg text-on-surface">Access denied</h1>
        <p className="font-body-md text-body-md text-on-surface-variant mt-2">
          You don&apos;t have permission to view this page with your current role
          {role ? ` (${role})` : ''}.
        </p>
        <div className="mt-6 flex justify-center gap-3">
          <Button variant="secondary" onClick={() => navigate(-1)}>
            Go back
          </Button>
          <Link to={home}>
            <Button>Return home</Button>
          </Link>
        </div>
      </div>
    </div>
  )
}
