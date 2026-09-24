import Button from './Button'
import Icon from './Icon'

// errorMessage: when the action the dialog was confirming turned out to be blocked
export default function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  variant = 'danger',
  loading = false,
  errorMessage,
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
          {!errorMessage && body && (
            <p className="mt-1.5 font-body-sm text-body-sm text-on-surface-variant">{body}</p>
          )}
          {errorMessage && (
            <div
              className="mt-3 flex items-start gap-2.5 rounded-xl border border-error/30 bg-error-container/60 px-4 py-3 font-body-sm text-body-sm text-on-error-container"
              role="alert"
            >
              <Icon name="alert" size={16} className="mt-0.5 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}
        </div>
        <div className="mt-5 flex justify-end gap-2.5">
          {errorMessage ? (
            <Button variant="secondary" onClick={onCancel}>
              OK
            </Button>
          ) : (
            <>
              <Button variant="secondary" onClick={onCancel}>
                {cancelLabel}
              </Button>
              <Button variant={variant} onClick={onConfirm} loading={loading}>
                {confirmLabel}
              </Button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}