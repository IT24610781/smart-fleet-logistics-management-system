import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import StatCard from '../../components/common/StatCard'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import { useAuth } from '../../context/AuthContext'
import { MOCK_DRIVER_TRIPS } from '../../utils/mockData'
import '../admin/AdminDashboard.css'

// Driver dashboard (section 22) — deliberately simpler than Admin/Dispatcher.
// Only information relevant to the driver's own work: no fleet-wide data.
export default function DriverDashboard() {
  const { user } = useAuth()
  const current = MOCK_DRIVER_TRIPS.find((t) => t.status === 'Assigned' || t.status === 'In Progress')
  const today = MOCK_DRIVER_TRIPS.filter((t) => t.status === 'Assigned' || t.status === 'In Progress')
  const completed = MOCK_DRIVER_TRIPS.filter((t) => t.status === 'Completed')
  const pending = MOCK_DRIVER_TRIPS.filter((t) => t.status === 'Assigned')

  return (
    <AppShell title="Dashboard">
      <div className="stack">
        <div>
          <h2 style={{ marginBottom: 2 }}>Welcome back{user?.firstName ? `, ${user.firstName}` : ''}</h2>
          <p style={{ color: 'var(--ink-500)', fontSize: 13 }}>Here's what's on your schedule.</p>
        </div>

        <div className="stat-grid">
          <StatCard label="Today's Trips" value={today.length} icon="route" />
          <StatCard label="Completed Trips" value={completed.length} icon="chart" />
          <StatCard label="Pending Trips" value={pending.length} icon="grid" />
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Current trip</h3>
          {current ? (
            <div className="activity-row" style={{ padding: 0 }}>
              <div className="activity-row-main">
                <span className="activity-row-title">{current.origin} → {current.destination}</span>
                <span className="activity-row-meta">{current.id} · {current.vehicle} · Scheduled {current.scheduledTime}</span>
              </div>
              <StatusBadge status={current.status} />
            </div>
          ) : (
            <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>No active or assigned trip right now.</p>
          )}
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Today's trips</h3>
          <div className="activity-list">
            {today.length === 0 && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>Nothing scheduled for today.</p>}
            {today.map((t) => (
              <div className="activity-row" key={t.id}>
                <div className="activity-row-main">
                  <span className="activity-row-title">{t.origin} → {t.destination}</span>
                  <span className="activity-row-meta">{t.id} · {t.scheduledTime}</span>
                </div>
                <StatusBadge status={t.status} />
              </div>
            ))}
          </div>
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Important actions</h3>
          <div className="quick-actions" style={{ maxWidth: 260 }}>
            <Link to="/driver/trips"><Button block variant="accent">View My Trips</Button></Link>
            <Link to="/profile"><Button block variant="secondary">View Profile</Button></Link>
          </div>
        </div>
      </div>
    </AppShell>
  )
}
