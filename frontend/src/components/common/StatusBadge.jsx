import './common.css'

const TONE = {
  active: 'success', available: 'success', completed: 'success', approved: 'success',
  pending: 'warning', 'in progress': 'warning', maintenance: 'warning', 'under maintenance': 'warning',
  assigned: 'info', 'in use': 'info', 'on trip': 'info',
  rejected: 'error', error: 'error', inactive: 'error', deactivated: 'error',
  offline: 'offline', idle: 'offline',
}

export default function StatusBadge({ status }) {
  const tone = TONE[String(status).toLowerCase()] || 'offline'
  return <span className={`badge badge-${tone}`}>{status}</span>
}