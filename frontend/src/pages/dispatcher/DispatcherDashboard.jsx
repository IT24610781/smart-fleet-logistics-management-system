import PageHeading from '../../components/common/PageHeading'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import StatCard from '../../components/common/StatCard'
import StatusBadge from '../../components/common/StatusBadge'
import DataTable from '../../components/common/DataTable'
import Button from '../../components/common/Button'
import { useAuth } from '../../context/AuthContext'
import { MOCK_TRIPS, FLEET_OVERVIEW, RECENT_ACTIVITY } from '../../utils/mockData'
import '../admin/AdminDashboard.css'

// Dispatcher dashboard (section 18) — same design system as Admin, but
// focused on daily logistics operations rather than system administration.
export default function DispatcherDashboard() {
  const { user } = useAuth()
  const pending = MOCK_TRIPS.filter((t) => t.status === 'Pending')
  const active = MOCK_TRIPS.filter((t) => t.status === 'In Progress')
  const completed = MOCK_TRIPS.filter((t) => t.status === 'Completed')

  const queueColumns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'route', header: 'Route', render: (r) => `${r.origin} → ${r.destination}` },
    { key: 'driver', header: 'Driver' },
    { key: 'vehicle', header: 'Vehicle', render: (r) => <span className="cell-id">{r.vehicle}</span> },
    { key: 'scheduledTime', header: 'Scheduled Time' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
  ]

  return (
    <AppShell title="Dashboard">
      <div className="fleet-dashboard-area stack">
        <PageHeading eyebrow="Dispatch operations" title="Dispatch overview" description={`Welcome back${user?.firstName ? `, ${user.firstName}` : ''}. Fleet availability and trip activity at a glance.`} />

        <div className="dashboard-stats">
          <StatCard label="Pending Trips" value={pending.length} icon="grid" description="Awaiting assignment" />
          <StatCard label="Active Trips" value={active.length} icon="route" description="Trips currently in progress" />
          <StatCard label="Completed Trips" value={completed.length} icon="chart" description="Completed journeys" />
          <StatCard label="Available Vehicles" value={FLEET_OVERVIEW.available} icon="truck" description="Ready for assignment" />
        </div>

        <div className="dash-grid">
          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Active operations</h3>
              <div className="activity-list">
                {active.map((t) => (
                  <div className="activity-row" key={t.id}>
                    <div className="activity-row-main">
                      <span className="activity-row-title">{t.origin} → {t.destination}</span>
                      <span className="activity-row-meta">{t.id} · {t.driver} · {t.vehicle}</span>
                    </div>
                    <StatusBadge status={t.status} />
                  </div>
                ))}
                {active.length === 0 && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>No trips in progress right now.</p>}
              </div>
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Trip queue</h3>
              <DataTable columns={queueColumns} rows={pending} rowKey="id" emptyTitle="No pending trips" />
            </div>
          </div>

          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Quick actions</h3>
              <div className="quick-actions">
                <Link to="/dispatcher/trips"><Button block variant="accent">+ Create Trip Request</Button></Link>
                <Link to="/dispatcher/trips"><Button block variant="secondary">View Active Trips</Button></Link>
                <Link to="/dispatcher/tracking"><Button block variant="secondary">Track Fleet</Button></Link>
                <Link to="/dispatcher/reports"><Button block variant="secondary">View Reports</Button></Link>
              </div>
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Fleet availability</h3>
              <div className="activity-list">
                <div className="activity-row"><StatusBadge status="Available" /><span className="cell-id">{FLEET_OVERVIEW.available} vehicles</span></div>
                <div className="activity-row"><StatusBadge status="In Use" /><span className="cell-id">{FLEET_OVERVIEW.inUse} vehicles</span></div>
                <div className="activity-row"><StatusBadge status="Maintenance" /><span className="cell-id">{FLEET_OVERVIEW.maintenance} vehicles</span></div>
              </div>
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Recent activity</h3>
              <div className="activity-list">
                {RECENT_ACTIVITY.slice(0, 4).map((a) => (
                  <div className="activity-row" key={a.id}>
                    <span className="activity-row-title" style={{ fontWeight: 500, fontSize: 12.5 }}>{a.text}</span>
                    <span className="activity-row-meta">{a.time}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  )
}
