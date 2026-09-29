import { useEffect } from 'react'
import { Link, useLocation } from 'react-router-dom'
import AppShell from './AppShell'
import Icon from '../common/Icon'
import '../../pages/admin/AdminDashboard.css'

export default function AdminShell({ title, children, dirty = false }) {
  useEffect(() => {
    if (!dirty) return
    const unload = event => { event.preventDefault(); event.returnValue = '' }
    const leave = event => {
      const anchor = event.target.closest('a[href]')
      if (anchor && !event.defaultPrevented && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0 && !window.confirm('Leave this page and discard your unsaved changes?')) {
        event.preventDefault(); event.stopPropagation()
      }
    }
    window.addEventListener('beforeunload',unload)
    document.addEventListener('click',leave,true)
    return () => { window.removeEventListener('beforeunload',unload); document.removeEventListener('click',leave,true) }
  }, [dirty])
  const { pathname } = useLocation()
  const parent = pathname.startsWith('/admin/drivers/') ? ['/admin/drivers', 'Drivers']
    : pathname.startsWith('/admin/vehicles/') ? ['/admin/vehicles', 'Vehicles'] : null
  return <AppShell title={title}><div className="admin-area">
    {pathname !== '/admin/dashboard' && <nav className="admin-breadcrumb" aria-label="Breadcrumb">
      <Link to="/admin/dashboard">Administration</Link><span aria-hidden="true">/</span>
      {parent && <><Link to={parent[0]}>{parent[1]}</Link><span aria-hidden="true">/</span></>}
      <span aria-current="page">{title}</span>
    </nav>}
    {parent && <Link className="admin-back" to={parent[0]}><Icon name="chevronLeft" size={16} />Back to {parent[1]}</Link>}
    {children}
  </div></AppShell>
}
