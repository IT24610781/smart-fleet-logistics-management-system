import Icon from './Icon'

export default function StatCard({ label, value, icon, description, delta }) {
  const deltaColor = delta?.direction === 'down' ? 'text-error' : 'text-secondary'
  return (
    <div className="dashboard-metric">
      {icon && <div className={`admin-metric-icon admin-metric-icon-${icon}`} aria-hidden="true"><Icon name={icon} size={46} /></div>}
      <div className="admin-metric-copy">
        <span className="admin-metric-label">{label}</span>
        <strong className="admin-metric-value">{value}</strong>
        {description && <span className="admin-muted">{description}</span>}
        {delta && <span className={`font-body-sm text-body-sm ${deltaColor}`}>{delta.text}</span>}
      </div>
    </div>
  )
}
