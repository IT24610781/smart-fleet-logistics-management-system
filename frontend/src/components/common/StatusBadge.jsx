// Status pill. Every tone below has a text/background contrast of at least
// 7:1 (WCAG AA needs 4.5:1), and each badge carries a small dot so the status
// is never conveyed by colour alone.
//
// Groups:  positive = ready / done      info   = assigned, waiting on someone
//          active   = moving right now  warning = needs attention
//          danger   = stopped / failed  neutral = idle, unknown
const TONES = {
  positive: 'bg-secondary-fixed text-on-secondary-fixed',
  info: 'bg-primary-fixed text-on-primary-fixed',
  active: 'bg-primary text-on-primary',
  warning: 'bg-amber-100 text-amber-900',
  danger: 'bg-error-container text-on-error-container',
  neutral: 'bg-surface-container text-on-surface-variant',
}

// Keys are lower-case with spaces: "In-Progress", "in_progress" and
// "In Progress" all normalise to 'in progress'.
const STATUS_TONE = {
  // vehicles
  available: 'positive',
  'on trip': 'active',
  'under maintenance': 'warning',
  maintenance: 'warning',
  // trips
  requested: 'warning',
  assigned: 'info',
  'in progress': 'active',
  completed: 'positive',
  cancelled: 'danger',
  // users / misc
  active: 'positive',
  approved: 'positive',
  pending: 'warning',
  'in use': 'active',
  rejected: 'danger',
  error: 'danger',
  inactive: 'danger',
  deactivated: 'danger',
  offline: 'neutral',
  idle: 'neutral',
  // fleet-map location freshness (SCRUM-128/129)
  fresh: 'positive',
  stale: 'warning',
  unavailable: 'neutral',

  // fleet utilization report (SCRUM-138/139)
  'limited data': 'warning',
}

function normalise(status) {
  return String(status ?? '').toLowerCase().replace(/[-_]+/g, ' ').trim()
}

export default function StatusBadge({ status }) {
  const tone = TONES[STATUS_TONE[normalise(status)]] || TONES.neutral
  return (
    <span className={`inline-flex items-center px-2.5 py-1 rounded-full font-label-md text-label-md capitalize ${tone}`}>
      <span aria-hidden="true" className="mr-1.5 h-1.5 w-1.5 rounded-full bg-current" />
      {status}
    </span>
  )
}