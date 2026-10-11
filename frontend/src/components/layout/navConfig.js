// Role-based navigation. One source of truth so Sidebar never has to
// branch on role internally — it just renders whatever list it's given.
export const NAV_BY_ROLE = {
  Admin: [
    { label: 'Dashboard', to: '/admin/dashboard', icon: 'grid' },
    { label: 'Drivers', to: '/admin/drivers', icon: 'users' },
    { label: 'Vehicles', to: '/admin/vehicles', icon: 'truck' },
    { label: 'Reports', to: '/admin/reports', icon: 'chart' },
    { label: 'Profile', to: '/profile', icon: 'user' },
  ],
  Dispatcher: [
    { label: 'Dashboard', to: '/dispatcher/dashboard', icon: 'grid' },
    { label: 'Trips', to: '/dispatcher/trips', icon: 'route', end: true },
    { label: 'Tracking', to: '/dispatcher/tracking', icon: 'pin' },
    { label: 'Trip History', to: '/dispatcher/trips/history', icon: 'check' },
    { label: 'Profile', to: '/profile', icon: 'user' },
  ],
  Driver: [
    { label: 'Dashboard', to: '/driver/dashboard', icon: 'grid' },
    { label: 'My Trips', to: '/driver/trips', icon: 'route', end: true },
    { label: 'Completed Trips', to: '/driver/trips/completed', icon: 'check' },
    { label: 'Profile', to: '/profile', icon: 'user' },
  ],
}

export const ROLE_LABEL = {
  Admin: 'Administrator',
  Dispatcher: 'Dispatcher',
  Driver: 'Driver',
}