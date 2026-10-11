import { useEffect, useMemo, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import PageHeading from '../../components/common/PageHeading'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import { useAuth } from '../../context/AuthContext'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

const PERIODS = [
  { key: 'all', label: 'All time' },
  { key: 'today', label: 'Today' },
  { key: 'week', label: 'This Week' },
  { key: 'month', label: 'This Month' },
]

// Same period convention as Driver's Completed Trips page: filters by
// completion time (updatedAt), "Today" is the local calendar day, Week/Month
// are rolling windows (last 7/30 days) rather than calendar week/month -
// simpler and timezone-safe.
function withinPeriod(value, period) {
  if (period === 'all') return true
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return false

  const now = new Date()
  if (period === 'today') {
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
    return date >= startOfToday
  }
  if (period === 'week') {
    return date >= new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000)
  }
  if (period === 'month') {
    return date >= new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000)
  }
  return true
}

function personName(person) {
  if (!person) return null
  const name = [person.firstName, person.lastName].filter(Boolean).join(' ')
  return name || person.username
}

export default function TripHistory() {
  const { user } = useAuth()
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [period, setPeriod] = useState('all')

  const [driversById, setDriversById] = useState({})

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const [allTrips, drivers] = await Promise.all([
          tripService.listTrips('Completed'),
          userService.listDrivers(),
        ])
        if (cancelled) return
        const mine = allTrips.filter((t) => t.createdByDispatcherId === user?.id)
        setTrips(mine)
        setDriversById(Object.fromEntries(drivers.map((d) => [d.id, d])))
      } catch (err) {
        if (!cancelled) setLoadError(err.message || 'Unable to load your trip history.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [reloadKey, user?.id])

  const visibleTrips = useMemo(
    () => trips.filter((t) => withinPeriod(t.updatedAt, period)),
    [trips, period]
  )

  function driverCell(row) {
    if (!row.assignedDriverId) return <span className="page-header-desc">—</span>
    const driver = driversById[row.assignedDriverId]
    if (!driver) return <span className="page-header-desc">—</span>
    return (
      <div>
        <div>{personName(driver)}</div>
        {driver.contactNumber && (
          <a
            href={`tel:${driver.contactNumber}`}
            className="flex items-center gap-1 text-primary hover:text-primary/80"
            onClick={(e) => e.stopPropagation()}
            title={`Call ${driver.contactNumber}`}
          >
            <Icon name="phone" size={13} />
            {driver.contactNumber}
          </a>
        )}
      </div>
    )
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'pickupLocation', header: 'Pickup' },
    { key: 'dropoffLocation', header: 'Drop-off' },
    { key: 'requiredCapacity', header: 'Capacity' },
    { key: 'driver', header: 'Driver', render: driverCell },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdAt', header: 'Created', render: (r) => formatDateTime(r.createdAt) },
    { key: 'updatedAt', header: 'Completed', render: (r) => formatDateTime(r.updatedAt) },
  ]

  return (
    <AppShell title="Trip History">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Dispatch operations" title="Trip History" />
          <p className="page-header-desc">Trips you created that have been completed.</p>
        </div>
      </div>

      <div className="trip-tabs">
        {PERIODS.map((p) => (
          <button
            key={p.key}
            className={`trip-tab${period === p.key ? ' active' : ''}`}
            onClick={() => setPeriod(p.key)}
          >
            {p.label}
          </button>
        ))}
      </div>

      {loadError ? (
        <div className="card card-pad" style={{ marginTop: 'var(--space-3)' }}>
          <ErrorMessage>{loadError}</ErrorMessage>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
          </div>
        </div>
      ) : (
        <div style={{ marginTop: 'var(--space-3)' }}>
          <DataTable
            columns={columns}
            rows={visibleTrips}
            loading={loading}
            rowKey="id"
            emptyTitle={period === 'all' ? 'No completed trips yet' : 'No completed trips in this period'}
            emptyDescription={period === 'all' ? 'Trips you created will show up here once they finish.' : 'Try a different time range.'}
          />
        </div>
      )}
    </AppShell>
  )
}