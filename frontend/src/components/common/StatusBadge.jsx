const TONE = {
  active: 'bg-secondary-container/50 text-on-secondary-container',
  available: 'bg-secondary-container/50 text-on-secondary-container',
  completed: 'bg-secondary-container/50 text-on-secondary-container',
  approved: 'bg-secondary-container/50 text-on-secondary-container',
  pending: 'bg-tertiary-container/40 text-on-tertiary-container',
  'in progress': 'bg-tertiary-container/40 text-on-tertiary-container',
  maintenance: 'bg-tertiary-container/40 text-on-tertiary-container',
  assigned: 'bg-primary-container/30 text-on-primary-container',
  'in use': 'bg-primary-container/30 text-on-primary-container',
  rejected: 'bg-error-container text-on-error-container',
  error: 'bg-error-container text-on-error-container',
  inactive: 'bg-error-container text-on-error-container',
  deactivated: 'bg-error-container text-on-error-container',
  offline: 'bg-surface-container text-on-surface-variant',
  idle: 'bg-surface-container text-on-surface-variant',
}

// One place mapping a status string to a tone, reused by every table/badge
// across Admin, Dispatcher and Driver pages.
export default function StatusBadge({ status }) {
  const tone = TONE[String(status).toLowerCase()] || 'bg-surface-container text-on-surface-variant'
  return (
    <span className={`inline-flex items-center px-2.5 py-1 rounded-full font-label-md text-label-md capitalize ${tone}`}>
      {status}
    </span>
  )
}
