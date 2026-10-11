import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import Icon from '../common/Icon'
import { NAV_BY_ROLE } from './navConfig'
export default function Topbar({ user, onOpenMobileMenu }) {
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const pages = NAV_BY_ROLE[user?.role] || []
  const mainPage = pages.some(page => page.to !== '/profile' && pathname.startsWith(page.to))
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const matches = pages.filter(page => page.label.toLowerCase().includes(query.trim().toLowerCase()))
  function search(event) { event.preventDefault(); if (matches[0]) { navigate(matches[0].to); setQuery(''); setOpen(false) } }
  return <header className="fleet-topbar">
    <button type="button" onClick={onOpenMobileMenu} className="lg:hidden p-2 rounded-lg text-on-surface-variant hover:bg-surface-container-low" aria-label="Open navigation"><Icon name="menu" size={22} /></button>
    {mainPage && <div className="fleet-page-search" onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false) }}>
      <form role="search" onSubmit={search}>
        <Icon name="search" size={19} />
        <input aria-label="Search pages" placeholder="Search pages..." value={query} onFocus={() => setOpen(true)} onChange={event => { setQuery(event.target.value); setOpen(true) }} onKeyDown={event => { if (event.key === 'Escape') { setOpen(false); event.currentTarget.blur() } }} aria-expanded={open} aria-controls="page-search-results" autoComplete="off" />
        <button type="submit" aria-label="Go to matching page" disabled={!matches.length}><Icon name="chevronRight" size={18} /></button>
      </form>
      {open && <div className="fleet-search-results" id="page-search-results">
        <span className="fleet-search-caption">Pages available to you</span>
        {matches.length ? matches.map(page => <Link key={page.to} to={page.to} onClick={() => { setOpen(false); setQuery('') }}><Icon name={page.icon} size={18} />{page.label}<Icon name="chevronRight" size={14} /></Link>) : <p>No matching pages.</p>}
      </div>}
    </div>}
  </header>
}