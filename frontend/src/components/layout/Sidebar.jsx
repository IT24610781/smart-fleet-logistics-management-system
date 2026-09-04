import { NavLink } from 'react-router-dom'
import Icon from '../common/Icon'
import { NAV_BY_ROLE } from './navConfig'
import './Sidebar.css'

export default function Sidebar({ role, onLogout, open }) {
  const items = NAV_BY_ROLE[role] || []

  return (
    <aside id="primary-sidebar" className={`sidebar${open ? ' open' : ''}`}>
      <div className="sidebar-brand">
        <img src="/fleet-mark.svg" alt="" />
        <div>
          <span className="sidebar-brand-text">Smart Fleet</span>
          <span className="sidebar-brand-tag">Fleet &amp; Logistics</span>
        </div>
      </div>

      <nav className="sidebar-nav" aria-label="Primary">
        {items.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) => `sidebar-link${isActive ? ' active' : ''}`}
          >
            <Icon name={item.icon} className="sidebar-icon" />
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="sidebar-foot">
        <button type="button" className="sidebar-logout" onClick={onLogout}>
          <Icon name="logout" className="sidebar-icon" />
          Logout
        </button>
      </div>
    </aside>
  )
}
