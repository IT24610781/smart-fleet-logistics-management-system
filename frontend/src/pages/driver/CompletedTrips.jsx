import { useEffect, useMemo, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'
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

function userLabel(user) {
  if (!user) return null
  const name = [user.firstName, user.lastName].filter(Boolean).join(' ')
  return name || user.username
}

// A map link that works whether or not this location has precise GPS
// coordinates: with coordinates it points straight at the exact point; a
// plain typed address falls back to a text search instead.
function mapLinkFor(address, lat, lng) {
  if (lat != null && lng != null) {
    return `https://www.google.com/maps?q=${lat},${lng}`
  }
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}`
}

function LocationCell({ address, lat, lng }) {
  return (
    <div className="flex items-center gap-1.5">
      <span>{address}</span>
      <a
        href={mapLinkFor(address, lat, lng)}
        target="_blank"
        rel="noopener noreferrer"
        onClick={(e) => e.stopPropagation()}
        className="text-primary hover:text-primary/80 shrink-0"
        title="Open in map"
        aria-label={`Open ${address} in map`}
      >
        <Icon name="pin" size={15} />
      </a>
    </div>
  )
}

export default function CompletedTrips() {
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [period, setPeriod] = useState('all')

  const [dispatchersById, setDispatchersById] = useState({})

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getMyTrips()
        const completed = data.filter((t) => t.status === 'Completed')
        if (cancelled) return
        setTrips(completed)

        // Resolve each distinct Dispatcher referenced by these trips, once -
        // a lookup-by-id endpoint, not a bulk list, so dedupe first.
        const dispatcherIds = [...new Set(completed.map((t) => t.createdByDispatcherId).filter(Boolean))]
        const results = await Promise.all(
          dispatcherIds.map((id) => userService.getUser(id).catch(() => null))
        )
        if (!cancelled) {
          setDispatchersById(Object.fromEntries(
            dispatcherIds.map((id, i) => [id, results[i]]).filter(([, u]) => u)
          ))
        }
      } catch (err) {
        if (!cancelled) setLoadError(err.message || 'Unable to load your completed trips.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  const visibleTrips = useMemo(
    () => trips.filter((t) => withinPeriod(t.updatedAt, period)),
    [trips, period]
  )

  function dispatcherCell(row) {
    if (!row.createdByDispatcherId) return <span className="page-header-desc">—</span>
    const dispatcher = dispatchersById[row.createdByDispatcherId]
    if (!dispatcher) return <span className="page-header-desc">—</span>
    return (
      <div>
        <div>{userLabel(dispatcher)}</div>
        {dispatcher.contactNumber && (
          <a
            href={`tel:${dispatcher.contactNumber}`}
            className="flex items-center gap-1 text-primary hover:text-primary/80"
            onClick={(e) => e.stopPropagation()}
            title={`Call ${dispatcher.contactNumber}`}
          >
            <Icon name="phone" size={13} />
            {dispatcher.contactNumber}
          </a>
        )}
      </div>
    )
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'pickupLocation', header: 'Pickup', render: (r) => <LocationCell address={r.pickupLocation} lat={r.pickupLat} lng={r.pickupLng} /> },
    { key: 'dropoffLocation', header: 'Drop-off', render: (r) => <LocationCell address={r.dropoffLocation} lat={r.dropoffLat} lng={r.dropoffLng} /> },
    { key: 'requiredCapacity', header: 'Capacity' },
    { key: 'dispatcher', header: 'Dispatcher', render: dispatcherCell },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'updatedAt', header: 'Completed', render: (r) => formatDateTime(r.updatedAt) },
  ]

  return (
    <AppShell title="Completed Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">Completed Trips</div>
          <p className="page-header-desc">Your finished trip history.</p>
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
            emptyDescription={period === 'all' ? 'Trips you finish will show up here.' : 'Try a different time range.'}
          />
        </div>
      )}
    </AppShell>
  )
}