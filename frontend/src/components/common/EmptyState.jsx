import Icon from './Icon'
import './common.css'

export default function EmptyState({ icon = 'grid', title, description, action }) {
  return (
    <div className="empty-state">
      <div className="empty-state-icon"><Icon name={icon} size={20} /></div>
      <div className="empty-state-title">{title}</div>
      {description && <div className="empty-state-desc">{description}</div>}
      {action}
    </div>
  )
}
