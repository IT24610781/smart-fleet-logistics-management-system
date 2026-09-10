export default function LoadingSpinner({ label = 'Loading…', center = false }) {
  return (
    <div
      className={`flex items-center gap-2.5 font-body-sm text-body-sm text-on-surface-variant ${
        center ? 'justify-center py-10' : ''
      }`}
      role="status"
      aria-live="polite"
    >
      <span className="spinner text-primary" aria-hidden="true" />
      <span>{label}</span>
    </div>
  )
}
