import Icon from './Icon'
import './common.css'

export function ErrorMessage({ children }) {
  if (!children) return null
  return (
    <div className="error-banner" role="alert">
      <Icon name="alert" size={15} className="icon" />
      <span>{children}</span>
    </div>
  )
}

export function SuccessMessage({ children }) {
  if (!children) return null
  return (
    <div className="success-banner" role="status">
      <Icon name="check" size={15} />
      <span>{children}</span>
    </div>
  )
}
