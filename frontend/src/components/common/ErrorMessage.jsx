import Icon from './Icon'

export function ErrorMessage({ children }) {
  if (!children) return null
  return (
    <div
      className="flex items-start gap-2.5 rounded-xl border border-error/30 bg-error-container/60 px-4 py-3 font-body-sm text-body-sm text-on-error-container"
      role="alert"
    >
      <Icon name="alert" size={16} className="mt-0.5 shrink-0" />
      <span>{children}</span>
    </div>
  )
}

export function SuccessMessage({ children }) {
  if (!children) return null
  return (
    <div
      className="flex items-start gap-2.5 rounded-xl border border-secondary/30 bg-secondary-container/40 px-4 py-3 font-body-sm text-body-sm text-on-secondary-container"
      role="status"
    >
      <Icon name="check" size={16} className="mt-0.5 shrink-0" />
      <span>{children}</span>
    </div>
  )
}
