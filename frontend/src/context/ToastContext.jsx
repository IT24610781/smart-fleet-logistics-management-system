import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import Icon from '../components/common/Icon'

const ToastContext = createContext(null)

const TOAST_STYLES = {
  success: {
    icon: 'check',
    className: 'toast-success',
  },
  error: {
    icon: 'alert',
    className: 'toast-error',
  },
  info: {
    icon: 'grid',
    className: 'toast-info',
  },
}

function Toast({ toast, onDismiss }) {
  const style = TOAST_STYLES[toast.type] || TOAST_STYLES.info

  return (
    <div className={`toast ${style.className}`} role={toast.type === 'error' ? 'alert' : 'status'}>
      <Icon name={style.icon} size={18} className="toast-icon" />
      <span className="toast-message">{toast.message}</span>
      <button
        type="button"
        className="toast-close"
        onClick={() => onDismiss(toast.id)}
        aria-label="Dismiss notification"
      >
        <Icon name="close" size={16} />
      </button>
    </div>
  )
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])

  const dismissToast = useCallback((id) => {
    setToasts((current) => current.filter((toast) => toast.id !== id))
  }, [])

  const showToast = useCallback((message, type = 'info', duration = 4000) => {
    const id = `${Date.now()}-${Math.random()}`
    setToasts((current) => [...current, { id, message, type }])

    window.setTimeout(() => dismissToast(id), duration)
    return id
  }, [dismissToast])

  useEffect(() => () => setToasts([]), [])

  return (
    <ToastContext.Provider value={{ showToast, dismissToast }}>
      {children}
      <div className="toast-viewport" aria-label="Notifications" aria-live="polite">
        {toasts.map((toast) => (
          <Toast key={toast.id} toast={toast} onDismiss={dismissToast} />
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within a ToastProvider')
  return context
}
