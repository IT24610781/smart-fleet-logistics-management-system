import { NavLink, useNavigate } from 'react-router-dom'
import Icon from '../common/Icon'
import { NAV_BY_ROLE, ROLE_LABEL } from './navConfig'
import { useAuth } from '../../context/AuthContext'

// Fixed left sidebar (Stitch "Precision Fleet Architecture" layout).
// Renders the same NAV_BY_ROLE config Topbar used to render, so adding a
// nav item is still a one-file change regardless of which shell renders it.
export default function Sidebar({ mobileOpen, onCloseMobile }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const items = NAV_BY_ROLE[user?.role] || []

  function handleLogout() {
    onCloseMobile?.()
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <>
      {mobileOpen && (
        <div
          className="fixed inset-0 bg-on-surface/40 z-40 lg:hidden"
          onClick={onCloseMobile}
          aria-hidden="true"
        />
      )}

      <aside
        className={`fixed left-0 top-0 bottom-0 w-64 bg-surface-container-lowest shadow-[0_1px_8px_rgba(0,0,0,0.08)] z-50 flex flex-col justify-between transition-transform duration-300 lg:translate-x-0 ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div>
          <div className="flex items-center gap-3 px-5 py-5 border-b border-outline-variant/40">
            <img src="/fleet-mark.svg" alt="" className="w-10 h-10 rounded-lg" />
            <div>
              <div className="flex items-center gap-2">
                <span className="font-headline-sm text-headline-sm text-on-surface">Smart Fleet</span>
              </div>
              <p className="font-label-sm text-label-sm uppercase tracking-wider text-on-surface-variant">
                Fleet &amp; Logistics
              </p>
            </div>
          </div>

          <nav className="flex flex-col gap-1 px-3 py-4" aria-label="Primary">
            {items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={onCloseMobile}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-xl font-title-md text-title-md transition-colors ${
                    isActive
                      ? 'bg-primary-container text-on-primary-container font-semibold'
                      : 'text-on-surface-variant hover:bg-surface-container-low'
                  }`
                }
              >
                <Icon name={item.icon} size={18} />
                {item.label}
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="px-3 py-4 border-t border-outline-variant/40">
          <div className="flex items-center gap-3 px-3 py-2 mb-1">
            <div className="w-9 h-9 rounded-full bg-primary text-on-primary flex items-center justify-center font-title-md text-body-sm">
              {(user?.firstName?.[0] || user?.username?.[0] || '?').toUpperCase()}
              {(user?.lastName?.[0] || '').toUpperCase()}
            </div>
            <div className="min-w-0">
              <p className="font-title-md text-body-sm text-on-surface truncate">
                {user?.firstName ? `${user.firstName} ${user.lastName || ''}`.trim() : user?.username}
              </p>
              <p className="font-label-sm text-label-sm text-on-surface-variant">
                {ROLE_LABEL[user?.role] || user?.role}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleLogout}
            className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl font-title-md text-title-md text-on-surface-variant hover:bg-error-container hover:text-on-error-container transition-colors"
          >
            <Icon name="logout" size={18} />
            Logout
          </button>
        </div>
      </aside>
    </>
  )
}
