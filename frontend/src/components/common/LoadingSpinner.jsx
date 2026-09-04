import './common.css'

export default function LoadingSpinner({ label = 'Loading…', center = false }) {
  return (
    <div className={`spinner-wrap${center ? ' center' : ''}`} role="status" aria-live="polite">
      <span className="spinner" aria-hidden="true" />
      <span>{label}</span>
    </div>
  )
}
