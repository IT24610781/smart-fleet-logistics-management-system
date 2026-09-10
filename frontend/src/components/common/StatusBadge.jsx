import './common.css'

// One place mapping a status string to a tone, reused by every table/badge
// across Admin, Dispatcher and Driver pages.
const TONE = {
  active: 'success', available: 'success', completed: 'success', approved: 'success',
  pending: 'warning', 'in progress': 'warning', maintenance: 'warning',
  assigned: 'info', 'in use': 'info',
  rejected: 'error', error: 'error', inactive: 'error', deactivated: 'error',
  offline: 'offline', idle: 'offline',
}

export default function StatusBadge({ status }) {
  const tone = TONE[String(status).toLowerCase()] || 'offline'
  return <span className={`badge badge-${tone}`}>{status}</span>
}
