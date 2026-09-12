const VARIANTS = {
  primary: 'bg-primary text-on-primary hover:bg-primary-container disabled:opacity-50',
  secondary:
    'bg-surface-container-low text-on-surface border border-outline-variant hover:bg-surface-container disabled:opacity-50',
  ghost: 'text-on-surface-variant hover:bg-surface-container-low disabled:opacity-50',
  danger: 'bg-error text-on-error hover:bg-error/90 disabled:opacity-50',
  // Kept for pages outside this migration's scope (Drivers, DispatcherDashboard,
  // DriverDashboard, Trips) that already call variant="accent" as their
  // call-to-action style.
  accent: 'bg-secondary text-on-secondary hover:bg-secondary-container hover:text-on-secondary-container disabled:opacity-50',
}

export default function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  loading = false,
  disabled = false,
  type = 'button',
  children,
  className = '',
  ...rest
}) {
  const sizeClasses = size === 'sm' ? 'px-3 py-1.5 text-body-sm' : 'px-4 py-2.5 text-body-md'

  return (
    <button
      type={type}
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center gap-2 rounded-xl font-title-md transition-colors ${sizeClasses} ${
        VARIANTS[variant]
      } ${block ? 'w-full' : ''} ${className}`}
      {...rest}
    >
      {loading && <span className="spinner" aria-hidden="true" />}
      {children}
    </button>
  )
}
