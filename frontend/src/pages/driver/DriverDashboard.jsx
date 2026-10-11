import { useEffect, useState } from 'react'
import PageHeading from '../../components/common/PageHeading'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import StatCard from '../../components/common/StatCard'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import { useAuth } from '../../context/AuthContext'
import * as tripService from '../../services/tripService'
import '../admin/AdminDashboard.css'

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

export default function DriverDashboard() {
  const { user } = useAuth()

  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getMyTrips()
        if (!cancelled) setTrips(data)
      } catch (err) {
        if (!cancelled) setLoadError(err.message || 'Unable to load your trips.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  const current = trips.find((t) => t.status === 'In-Progress') || trips.find((t) => t.status === 'Assigned')
  const active = trips.filter((t) => t.status === 'Assigned' || t.status === 'In-Progress')
  const completed = trips.filter((t) => t.status === 'Completed')
  const pending = trips.filter((t) => t.status === 'Assigned')

  return (
    <AppShell title="Dashboard">
      <div className="fleet-dashboard-area stack">
        <PageHeading eyebrow="Driver operations" title="Your driving overview" description={`Welcome back${user?.firstName ? `, ${user.firstName}` : ''}. Here is what's currently assigned to you.`} />

        {loadError ? (
          <div className="card card-pad">
            <ErrorMessage>{loadError}</ErrorMessage>
            <div style={{ marginTop: 'var(--space-3)' }}>
              <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
            </div>
          </div>
        ) : loading ? (
          <div className="card card-pad">
            <LoadingSpinner label="Loading your trips…" center />
          </div>
        ) : (
          <>
            <div className="dashboard-stats dashboard-stats-driver">
              <StatCard label="Active Trips" value={active.length} icon="route" description="Assigned and in-progress trips" />
              <StatCard label="Completed Trips" value={completed.length} icon="chart" description="Completed journeys" />
              <StatCard label="Pending Trips" value={pending.length} icon="grid" description="Assignments awaiting your response" />
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Current trip</h3>
              {current ? (
                <div className="activity-row" style={{ padding: 0 }}>
                  <div className="activity-row-main">
                    <span className="activity-row-title">{current.pickupLocation} → {current.dropoffLocation}</span>
                    <span className="activity-row-meta">{current.id.slice(0, 8)} · Updated {formatDateTime(current.updatedAt)}</span>
                  </div>
                  <StatusBadge status={current.status} />
                </div>
              ) : (
                <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>No active or assigned trip right now.</p>
              )}
            </div>

            <div className="card card-pad">
              <h3 className="section-title">Active trips</h3>
              <div className="activity-list">
                {active.length === 0 && <p style={{ fontSize: 13, color: 'var(--ink-500)' }}>Nothing assigned or in progress right now.</p>}
                {active.map((t) => (
                  <div className="activity-row" key={t.id}>
                    <div className="activity-row-main">
                      <span className="activity-row-title">{t.pickupLocation} → {t.dropoffLocation}</span>
                      <span className="activity-row-meta">{t.id.slice(0, 8)} · Updated {formatDateTime(t.updatedAt)}</span>
                    </div>
                    <StatusBadge status={t.status} />
                  </div>
                ))}
              </div>
            </div>
          </>
        )}

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