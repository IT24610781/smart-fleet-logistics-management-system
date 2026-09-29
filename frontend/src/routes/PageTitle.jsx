import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

// One place that gives every screen its own browser-tab title (helps
// accessibility, history and bookmarks). Add new routes to this map.
const TITLES = {
  '/login': 'Sign in',
  '/forgot-password': 'Reset password',
  '/profile/password': 'Change password',
  '/register/dispatcher': 'Create account',
  '/403': 'Access denied',
  '/admin/dashboard': 'Admin dashboard',
  '/admin/drivers': 'Drivers',
  '/admin/drivers/create': 'Add driver',
  '/admin/vehicles': 'Vehicles',
  '/admin/vehicles/create': 'Add vehicle',
  '/admin/reports': 'Reports',
  '/dispatcher/dashboard': 'Dispatcher dashboard',
  '/dispatcher/trips': 'Trips',
  '/dispatcher/tracking': 'Live tracking',
  '/dispatcher/reports': 'Reports',
  '/driver/dashboard': 'Driver dashboard',
  '/driver/trips': 'My trips',
  '/profile': 'My profile',
}

function titleFor(pathname) {
  if (/^\/admin\/vehicles\/[^/]+\/edit\/?$/.test(pathname)) return 'Edit vehicle'
  return TITLES[pathname.replace(/\/+$/, '') || '/']
}

export default function PageTitle() {
  const { pathname } = useLocation()

  useEffect(() => {
    const title = titleFor(pathname)
    document.title = title ? `${title} · Smart Fleet` : 'Smart Fleet — Fleet & Logistics Management'
  }, [pathname])

  return null
}
