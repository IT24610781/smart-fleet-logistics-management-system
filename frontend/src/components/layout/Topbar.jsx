import { useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import Icon from '../common/Icon'
import { NAV_BY_ROLE, ROLE_LABEL } from './navConfig'
import { useAuth } from '../../context/AuthContext'
import './Topbar.css'

function initials(user) {
  const first = user?.firstName?.[0] || user?.username?.[0] || '?'
  const last = user?.lastName?.[0] || ''
  return (first + last).toUpperCase()
}

export default function Topbar({ title, user }) {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const [mobileOpen, setMobileOpen] = useState(false)

  const navItems = NAV_BY_ROLE[user?.role] || []

  function handleLogout() {
    setMobileOpen(false)
    logout()
    navigate('/login', { replace: true })
  }

  function closeMobileMenu() {
    setMobileOpen(false)
  }

  return (
    <header className="topbar">
      <div className="topbar-inner">

        {/* Brand */}
        <NavLink
          to={
            user?.role === 'Admin'
              ? '/admin/dashboard'
              : user?.role === 'Dispatcher'
                ? '/dispatcher/dashboard'
                : '/driver/dashboard'
          }
          className="topbar-brand"
          onClick={closeMobileMenu}
        >
          <img src="/fleet-mark.svg" alt="" className="topbar-brand-icon" />

          <div className="topbar-brand-text">
            <span className="topbar-brand-name">Smart Fleet</span>
            <span className="topbar-brand-subtitle">
              Fleet &amp; Logistics
            </span>
          </div>
        </NavLink>

        {/* Desktop Navigation */}
        <nav className="topbar-nav" aria-label="Primary navigation">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `topbar-nav-link${isActive ? ' active' : ''}`
              }
            >
              <Icon name={item.icon} className="topbar-nav-icon" />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>

        {/* Right side */}
        <div className="topbar-actions">

          <NavLink
            to="/profile"
            className="topbar-user"
            onClick={closeMobileMenu}
          >
            <div className="topbar-avatar">
              {initials(user)}
            </div>

            <div className="topbar-user-meta">
              <span className="topbar-user-name">
                {user?.firstName
                  ? `${user.firstName} ${user.lastName || ''}`.trim()
                  : user?.username}
              </span>

              <span className="topbar-user-role">
                {ROLE_LABEL[user?.role] || user?.role}
              </span>
            </div>
          </NavLink>

          <button
            type="button"
            className="topbar-logout"
            onClick={handleLogout}
            title="Logout"
          >
            <Icon name="logout" />
            <span>Logout</span>
          </button>

          {/* Mobile menu */}
          <button
            type="button"
            className="topbar-mobile-btn"
            onClick={() => setMobileOpen((value) => !value)}
            aria-label={mobileOpen ? 'Close navigation' : 'Open navigation'}
            aria-expanded={mobileOpen}
          >
            <Icon name={mobileOpen ? 'close' : 'menu'} />
          </button>
        </div>
      </div>

      {/* Mobile Navigation */}
      {mobileOpen && (
        <div className="topbar-mobile-menu">
          <div className="topbar-mobile-title">
            {title}
          </div>

          <nav aria-label="Mobile navigation">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                onClick={closeMobileMenu}
                className={({ isActive }) =>
                  `topbar-mobile-link${isActive ? ' active' : ''}`
                }
              >
                <Icon name={item.icon} />
                <span>{item.label}</span>
              </NavLink>
            ))}
          </nav>

          <button
            type="button"
            className="topbar-mobile-logout"
            onClick={handleLogout}
          >
            <Icon name="logout" />
            <span>Logout</span>
          </button>
        </div>
      )}
    </header>
  )
}