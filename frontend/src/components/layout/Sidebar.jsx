import { useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import Icon from '../common/Icon'
import { NAV_BY_ROLE } from './navConfig'
import { useAuth } from '../../context/AuthContext'
const descriptions = { Dashboard:'Your fleet overview and daily operations.', Drivers:'View Driver accounts and manage their availability.', Vehicles:'Manage your vehicle registry and fleet availability.', Trips:'Create requests and manage assignments.', 'My Trips':'View assigned trips and respond to requests.', 'Completed Trips':'Your finished trip history.', Tracking:'Open the fleet tracking page.', Reports:'Open the reporting page.' }
export default function Sidebar({ mobileOpen, onCloseMobile }) {
  const { user } = useAuth()
  const [dismissed, setDismissed] = useState(null)
  const dashboard = NAV_BY_ROLE[user?.role]?.[0]?.to || '/'
  const items = (NAV_BY_ROLE[user?.role] || []).filter(item => item.to !== '/profile')
  return <>
    {mobileOpen && <div className="fixed inset-0 bg-on-surface/40 z-40 lg:hidden" onClick={onCloseMobile} aria-hidden="true" />}
    <aside className={`fleet-sidebar fixed left-0 top-0 bottom-0 w-64 z-50 flex flex-col justify-between transition-transform duration-300 lg:translate-x-0 ${mobileOpen ? 'translate-x-0' : '-translate-x-full'}`}>
      <div>
        <Link to={dashboard} onClick={onCloseMobile} aria-label="Smart Fleet dashboard" className="fleet-brand flex items-center gap-3 px-5 py-5 border-b border-white/10">
          <img src="/fleet-mark.svg" alt="" className="w-10 h-10 rounded-lg" />
          <div><span className="fleet-brand-name">Smart Fleet</span><p className="fleet-brand-caption">Fleet &amp; Logistics</p></div>
        </Link>
        <nav className="flex flex-col gap-2 px-3 py-5" aria-label="Primary">
          {items.map((item,index) => <div className={`fleet-nav-item ${dismissed === index ? 'is-dismissed' : ''}`} key={item.to} onMouseEnter={() => setDismissed(null)} onFocus={() => setDismissed(null)} onKeyDown={event => { if (event.key === 'Escape') setDismissed(index) }}>
            <NavLink to={item.to} end={item.end} onClick={onCloseMobile} aria-describedby={`nav-tip-${index}`} className={({ isActive }) => `fleet-nav-link ${isActive ? 'is-active' : ''}`}>
              <span className={`fleet-nav-icon fleet-nav-color-${index % 5}`}><Icon name={item.icon} size={20} /></span>
              <span>{item.label}</span><Icon name="chevronRight" size={14} className="fleet-nav-arrow" />
            </NavLink>
            <div className="fleet-nav-preview" id={`nav-tip-${index}`} role="tooltip">
              <div className={`fleet-preview-art fleet-nav-color-${index % 5}`}><Icon name={item.icon} size={34} /></div>
              <strong>{item.label}</strong><p>{descriptions[item.label]}</p>
            </div>
          </div>)}
        </nav>
      </div>
      <div className="px-3 py-4 border-t border-white/10">
        <NavLink to="/profile" onClick={onCloseMobile} className={({ isActive }) => `fleet-nav-link fleet-profile-link ${isActive ? 'is-active' : ''}`}>
          <span className="fleet-nav-icon fleet-nav-color-4"><Icon name="user" size={22} /></span>
          <span><strong>Profile</strong><small>{user?.username || 'Your account'}</small></span>
        </NavLink>
      </div>
    </aside>
  </>
}