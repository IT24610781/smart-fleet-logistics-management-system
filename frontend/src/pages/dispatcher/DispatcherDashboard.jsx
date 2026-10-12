import PageHeading from '../../components/common/PageHeading'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import StatCard from '../../components/common/StatCard'
import StatusBadge from '../../components/common/StatusBadge'
import DataTable from '../../components/common/DataTable'
import Button from '../../components/common/Button'
import { useAuth } from '../../context/AuthContext'
import { listDrivers } from '../../services/userService'
import { listVehicles } from '../../services/vehicleService'
import { listTrips } from '../../services/tripService'
import '../admin/AdminDashboard.css'

const date = (value) => (value && !Number.isNaN(new Date(value).getTime()) ? new Date(value).toLocaleString() : 'Unavailable')

export default function DispatcherDashboard() {
  const { user } = useAuth()
  const [sources, setSources] = useState({ drivers: null, vehicles: null, trips: null })
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    Promise.allSettled([listDrivers(), listVehicles(), listTrips()]).then((results) => {
      if (cancelled) return
      const data = {}, failures = {}
      results.forEach((result, i) => {
        const key = ['drivers', 'vehicles', 'trips'][i]
        data[key] = result.status === 'fulfilled' ? result.value : null
        if (result.status === 'rejected') failures[key] = result.reason?.message || 'Unable to retrieve data.'
      })
      setSources(data)
      setErrors(failures)
      setLoading(false)
    })
    return () => { cancelled = true }
  }, [])

  const { drivers, vehicles, trips } = sources

  const driverName = (id) => {
    const d = drivers?.find((driver) => driver.id === id)
    return d ? `${d.firstName} ${d.lastName}` : 'Not assigned'
  }
  const vehicleName = (id) => vehicles?.find((v) => v.id === id)?.registrationNo || 'Not assigned'

  const pending = trips?.filter((t) => t.status === 'Requested') || []
  const active = trips?.filter((t) => t.status === 'In-Progress') || []
  const completed = trips?.filter((t) => t.status === 'Completed') || []
  const availableVehicles = vehicles?.filter((v) => v.status === 'Available').length

  const recentTrips = [...(trips || [])].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0, 4)

  const queueColumns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'route', header: 'Route', render: (r) => `${r.pickupLocation} → ${r.dropoffLocation}` },
    { key: 'assignedDriverId', header: 'Driver', render: (r) => driverName(r.assignedDriverId) },
    { key: 'assignedVehicleId', header: 'Vehicle', render: (r) => <span className="cell-id">{vehicleName(r.assignedVehicleId)}</span> },
    { key: 'createdAt', header: 'Requested', render: (r) => date(r.createdAt) },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
  ]

  return (
    <AppShell title="Dashboard">
      <div className="fleet-dashboard-area stack">
        <PageHeading
          eyebrow="Dispatch operations"
          title="Dispatch overview"
          description={`Welcome back${user?.firstName ? `, ${user.firstName}` : ''}. Fleet availability and trip activity at a glance.`}
        />

        {!loading && Object.keys(errors).length > 0 && (
          <div className="admin-source-error" role="alert">
            <strong>Some fleet data is unavailable.</strong>
            {Object.entries(errors).map(([key, error]) => (
              <p key={key}>{key}: {error}</p>
            ))}
            <p>Available sections remain visible. Reload this page to try again.</p>
          </div>
        )}

        <div className="dashboard-stats">
          <StatCard label="Pending Trips" value={loading ? '...' : pending.length} icon="grid" description="Awaiting assignment" />
          <StatCard label="Active Trips" value={loading ? '...' : active.length} icon="route" description="Trips currently in progress" />
          <StatCard label="Completed Trips" value={loading ? '...' : completed.length} icon="chart" description="Completed journeys" />
          <StatCard label="Available Vehicles" value={loading ? '...' : availableVehicles ?? '--'} icon="truck" description="Ready for assignment" />
        </div>
        <div className="dash-grid">
          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Active operations</h3>
              <div className="activity-list">
                {active.map((t) => (
                  <div className="activity-row" key={t.id}>
                    <div className="activity-row-main">
                      <span className="activity-row-title">{t.pickupLocation} → {t.dropoffLocation}</span>
                      <span className="activity-row-meta">{t.id.slice(0, 8)} · {driverName(t.assignedDriverId)} · {vehicleName(t.assignedVehicleId)}</span>
                    </div>
                    <StatusBadge status={t.status} />
                  </div>
                ))}
                {!loading && active.length === 0 && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>No trips in progress right now.</p>}
                {loading && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>Loading...</p>}
              </div>
            </div>
            <div className="card card-pad">
              <h3 className="section-title">Trip queue</h3>
              <DataTable columns={queueColumns} rows={pending} rowKey="id" loading={loading} emptyTitle="No pending trips" />
            </div>
          </div>
          <div className="stack">
            <div className="card card-pad">
              <h3 className="section-title">Quick actions</h3>
              <div className="quick-actions">
                <Link to="/dispatcher/trips"><Button block variant="accent">+ Create Trip Request</Button></Link>
                <Link to="/dispatcher/trips"><Button block variant="secondary">View Active Trips</Button></Link>
                <Link to="/dispatcher/tracking"><Button block variant="secondary">Track Fleet</Button></Link>
                <Link to="/dispatcher/trips/history"><Button block variant="secondary">Trip History</Button></Link>
              </div>
            </div>
            <div className="card card-pad">
              <h3 className="section-title">Fleet availability</h3>
              {loading || !vehicles ? (
                <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>{loading ? 'Loading...' : 'Vehicle data unavailable.'}</p>
              ) : (
                <div className="activity-list">
                  {['Available', 'On Trip', 'Under Maintenance'].map((status) => (
                    <div className="activity-row" key={status}>
                      <StatusBadge status={status} />
                      <span className="cell-id">{vehicles.filter((v) => v.status === status).length} vehicles</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
            <div className="card card-pad">
              <h3 className="section-title">Recent activity</h3>
              <div className="activity-list">
                {recentTrips.map((t) => (
                  <div className="activity-row" key={t.id}>
                    <span className="activity-row-title" style={{ fontWeight: 500, fontSize: 12.5 }}>
                      {t.pickupLocation} → {t.dropoffLocation} ({t.status})
                    </span>
                    <span className="activity-row-meta">{date(t.createdAt)}</span>
                  </div>
                ))}
                {!loading && recentTrips.length === 0 && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>No recent trip activity.</p>}
                {loading && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>Loading...</p>}
              </div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  )
}