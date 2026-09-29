import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import Icon from '../components/common/Icon'

const ToastContext = createContext(null)
const TOAST_STYLES = {
  success: { icon: 'check', title: 'Success', className: 'toast-success' },
  error: { icon: 'alert', title: 'Action needed', className: 'toast-error' },
  info: { icon: 'info', title: 'Notification', className: 'toast-info' },
  warning: { icon: 'alert', title: 'Please note', className: 'toast-warning' },
}

function Toast({ toast, onDismiss }) {
  const style = TOAST_STYLES[toast.type] || TOAST_STYLES.info
  const [hovered, setHovered] = useState(false)
  const [focused, setFocused] = useState(false)
  const remaining = useRef(toast.duration)
  const paused = hovered || focused
  useEffect(() => {
    if (paused || toast.duration <= 0) return
    const started = Date.now()
    const timer = window.setTimeout(() => onDismiss(toast.id), remaining.current)
    return () => {
      window.clearTimeout(timer)
      remaining.current = Math.max(0, remaining.current - (Date.now() - started))
    }
  }, [paused, toast.duration, toast.id, onDismiss])
  return <div className={`toast ${style.className}`} role={toast.type === 'error' ? 'alert' : 'status'} aria-atomic="true"
    onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}
    onFocusCapture={() => setFocused(true)} onBlurCapture={event => { if (!event.currentTarget.contains(event.relatedTarget)) setFocused(false) }}>
    <span className="toast-icon-box"><Icon name={style.icon} size={20} /></span>
    <div className="toast-copy"><strong className="toast-title">{style.title}</strong><p className="toast-message">{toast.message}</p></div>
    <button type="button" className="toast-close" onClick={() => onDismiss(toast.id)} aria-label="Dismiss notification"><Icon name="close" size={17} /></button>
    {toast.duration > 0 && <span className="toast-progress" aria-hidden="true" style={{ animationDuration: `${toast.duration}ms`, animationPlayState: paused ? 'paused' : 'running' }} />}
  </div>
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])
  const dismissToast = useCallback(id => setToasts(current => current.filter(toast => toast.id !== id)), [])
  const showToast = useCallback((message, type = 'info', duration) => {
    const id = `${Date.now()}-${Math.random()}`
    const lifetime = Number.isFinite(duration) ? Math.max(0, duration) : type === 'error' ? 7000 : 4500
    setToasts(current => [...current.slice(-3), { id, message, type, duration: lifetime }])
    return id
  }, [])
  return <ToastContext.Provider value={{ showToast, dismissToast }}>
    {children}
    <div className="toast-viewport" aria-label="Notifications">
      {toasts.map(toast => <Toast key={toast.id} toast={toast} onDismiss={dismissToast} />)}
    </div>
  </ToastContext.Provider>
}

export function useToast() {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast must be used within a ToastProvider')
  return context
}
