import { Link } from 'react-router-dom'
import Icon from '../common/Icon'

// Now a slim in-content header inside AppShell (nav itself moved into
// Sidebar). Keeps the page title + quick link to Profile visible on every
// authenticated page, and owns the mobile hamburger that opens Sidebar.
export default function Topbar({ title, user, onOpenMobileMenu }) {
  return (
    <header className="sticky top-0 z-30 flex items-center justify-between gap-4 px-4 sm:px-6 py-4 bg-surface-container-lowest border-b border-outline-variant/40">
      <div className="flex items-center gap-3 min-w-0">
        <button
          type="button"
          onClick={onOpenMobileMenu}
          className="lg:hidden p-2 -ml-2 rounded-lg text-on-surface-variant hover:bg-surface-container-low"
          aria-label="Open navigation"
        >
          <Icon name="menu" size={20} />
        </button>
        <h1 className="font-headline-sm text-headline-sm text-on-surface truncate">{title}</h1>
      </div>

      <Link
        to="/profile"
        className="flex items-center gap-2 px-2 py-1.5 rounded-xl hover:bg-surface-container-low transition-colors"
      >
        <div className="w-8 h-8 rounded-full bg-primary text-on-primary flex items-center justify-center font-title-md text-label-md">
          {(user?.firstName?.[0] || user?.username?.[0] || '?').toUpperCase()}
        </div>
        <span className="hidden sm:inline font-title-md text-body-sm text-on-surface">
          {user?.firstName ? `${user.firstName} ${user.lastName || ''}`.trim() : user?.username}
        </span>
      </Link>
    </header>
  )
}
