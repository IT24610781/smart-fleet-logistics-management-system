import './common.css'

export default function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  loading = false,
  disabled = false,
  type = 'button',
  children,
  ...rest
}) {
  const classes = [
    'btn',
    `btn-${variant}`,
    size === 'sm' ? 'btn-sm' : '',
    block ? 'btn-block' : '',
  ].filter(Boolean).join(' ')

  return (
    <button type={type} className={classes} disabled={disabled || loading} {...rest}>
      {loading && <span className="spinner" aria-hidden="true" style={{ width: 14, height: 14, borderWidth: 2 }} />}
      {children}
    </button>
  )
}
