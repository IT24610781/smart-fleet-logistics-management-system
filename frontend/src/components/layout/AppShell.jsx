import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import Sidebar from './Sidebar'
import Topbar from './Topbar'

// Shared authenticated layout: Sidebar + Topbar + <children>, matching the
// Stitch "Precision Fleet Architecture" shell. Same usage as before
// (<AppShell title="...">...</AppShell>) so no page needs to change how it
// calls this.
export default function AppShell({ title, children }) {
  const { user } = useAuth()
  const [mobileOpen, setMobileOpen] = useState(false)
  const { pathname } = useLocation()
  const isDashboard = ['/admin/dashboard', '/dispatcher/dashboard', '/driver/dashboard'].includes(pathname)

  return (
    <div className="fleet-app-shell min-h-screen bg-background">
      <Sidebar mobileOpen={mobileOpen} onCloseMobile={() => setMobileOpen(false)} />

      <div className="lg:pl-64">
        <Topbar title={title} user={user} onOpenMobileMenu={() => setMobileOpen(true)} />
        <main className={`p-4 sm:p-6 lg:p-8${isDashboard ? ' dashboard-main' : ''}`}>{children}</main>
      </div>
    </div>
  )
}
