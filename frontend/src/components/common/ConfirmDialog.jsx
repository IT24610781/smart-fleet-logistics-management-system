import Button from './Button'
import './common.css'

export default function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  variant = 'danger',
  loading = false,
  onConfirm,
  onCancel,
}) {
  if (!open) return null
  return (
    <div className="dialog-backdrop" onClick={onCancel}>
      <div className="dialog-panel" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
        <div>
          <div className="dialog-title">{title}</div>
          {body && <p className="dialog-body" style={{ marginTop: 6 }}>{body}</p>}
        </div>
        <div className="dialog-actions">
          <Button variant="secondary" onClick={onCancel}>{cancelLabel}</Button>
          <Button variant={variant} onClick={onConfirm} loading={loading}>{confirmLabel}</Button>
        </div>
      </div>
    </div>
  )
}
