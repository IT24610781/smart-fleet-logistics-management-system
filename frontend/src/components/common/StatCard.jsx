import Icon from './Icon'
import './common.css'

export default function StatCard({ label, value, icon, delta }) {
  return (
    <div className="stat-card">
      <div className="stat-card-top">
        <span className="stat-card-label">{label}</span>
        {icon && <span className="stat-card-icon"><Icon name={icon} size={15} /></span>}
      </div>
      <span className="stat-card-value">{value}</span>
      {delta && <span className={`stat-card-delta ${delta.direction}`}>{delta.text}</span>}
    </div>
  )
}
