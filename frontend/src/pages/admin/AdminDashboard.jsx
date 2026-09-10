import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import StatCard from '../../components/common/StatCard'
import StatusBadge from '../../components/common/StatusBadge'
import DataTable from '../../components/common/DataTable'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { useAuth } from '../../context/AuthContext'
import { MOCK_ADMIN_STATS, MOCK_TRIPS, FLEET_OVERVIEW, DRIVER_OVERVIEW, RECENT_ACTIVITY } from '../../utils/mockData'
import './AdminDashboard.css'

const ACTIVITY_ICON = { driver: 'users', trip: 'route', vehicle: 'truck', maintenance: 'alert' }

// Primary Admin dashboard (sections 10-12). KPI cards, Fleet Overview,
// Driver Overview, a mixed Recent Activity feed, a Recent Trips table with
// the exact columns the spec asks for, and Quick Actions. All demo data —
// clearly separated in src/utils/mockData.js until the real services exist.
export default function AdminDashboard() {
  const { user } = useAuth()
  const s = MOCK_ADMIN_STATS

  const tripColumns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'driver', header: 'Driver' },
    { key: 'vehicle', header: 'Vehicle', render: (r) => <span className="cell-id">{r.vehicle}</span> },
    { key: 'route', header: 'Route', render: (r) => `${r.origin} → ${r.destination}` },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdDate', header: 'Date' },
  ]

  return (
    <AppShell title="Dashboard">
      <div className="stack">
        <div>
          <h2 style={{ marginBottom: 2 }}>Welcome back{user?.firstName ? `, ${user.firstName}` : ''}</h2>
          <p style={{ color: 'var(--ink-500)', fontSize: 13 }}>Here's what's happening across the fleet today.</p>
        </div>

        <div className="stat-grid">
          <StatCard label="Total Drivers" value={s.totalDrivers.value} icon="users" delta={s.totalDrivers.delta} />
          <StatCard label="Total Vehicles" value={s.totalVehicles.value} icon="truck" delta={s.totalVehicles.delta} />
          <StatCard label="Available Vehicles" value={s.availableVehicles.value} icon="grid" delta={s.availableVehicles.delta} />
          <StatCard label="Active Trips" value={s.activeTrips.value} icon="route" delta={s.activeTrips.delta} />
        </div>

        <div className="dash-grid">
          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Recent trips</h3>
              <DataTable columns={tripColumns} rows={MOCK_TRIPS.slice(0, 5)} rowKey="id" emptyTitle="No trips yet" />
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Recent activity</h3>
              <div className="activity-list">
                {RECENT_ACTIVITY.map((a) => (
                  <div className="activity-row" key={a.id}>
                    <div className="activity-row-main" style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
                      <span className="activity-icon"><Icon name={ACTIVITY_ICON[a.type] || 'grid'} size={13} /></span>
                      <span className="activity-row-title" style={{ fontWeight: 500 }}>{a.text}</span>
                    </div>
                    <span className="activity-row-meta">{a.time}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Quick actions</h3>
              <div className="quick-actions">
                <Link to="/admin/drivers/create"><Button block variant="accent">+ Create Driver</Button></Link>
                <Link to="/admin/drivers"><Button block variant="secondary">View Drivers</Button></Link>
                <Link to="/admin/vehicles"><Button block variant="secondary">View Vehicles</Button></Link>
                <Link to="/admin/reports"><Button block variant="secondary">View Reports</Button></Link>
              </div>
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Fleet overview</h3>
              <div className="activity-list">
                <div className="activity-row"><StatusBadge status="Available" /><span className="cell-id">{FLEET_OVERVIEW.available} vehicles</span></div>
                <div className="activity-row"><StatusBadge status="In Use" /><span className="cell-id">{FLEET_OVERVIEW.inUse} vehicles</span></div>
                <div className="activity-row"><StatusBadge status="Maintenance" /><span className="cell-id">{FLEET_OVERVIEW.maintenance} vehicles</span></div>
                <div className="activity-row"><StatusBadge status="Inactive" /><span className="cell-id">{FLEET_OVERVIEW.inactive} vehicles</span></div>
              </div>
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Driver overview</h3>
              <div className="activity-list">
                <div className="activity-row"><span>Total drivers</span><span className="cell-id">{DRIVER_OVERVIEW.total}</span></div>
                <div className="activity-row"><StatusBadge status="Active" /><span className="cell-id">{DRIVER_OVERVIEW.active} drivers</span></div>
                <div className="activity-row"><StatusBadge status="Inactive" /><span className="cell-id">{DRIVER_OVERVIEW.inactive} drivers</span></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  )
}
