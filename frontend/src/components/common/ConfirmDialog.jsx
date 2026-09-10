import Button from './Button'

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
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-on-surface/40 p-4" onClick={onCancel}>
      <div
        className="w-full max-w-sm rounded-xl bg-surface-container-lowest p-5 shadow-xl"
        role="dialog"
        aria-modal="true"
        onClick={(e) => e.stopPropagation()}
      >
        <div>
          <div className="font-headline-sm text-headline-sm text-on-surface">{title}</div>
          {body && <p className="mt-1.5 font-body-sm text-body-sm text-on-surface-variant">{body}</p>}
        </div>
        <div className="mt-5 flex justify-end gap-2.5">
          <Button variant="secondary" onClick={onCancel}>
            {cancelLabel}
          </Button>
          <Button variant={variant} onClick={onConfirm} loading={loading}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  )
}
