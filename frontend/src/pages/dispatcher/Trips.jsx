import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'
import './Trips.css'

const TABS = ['All', 'Requested', 'Assigned', 'In-Progress', 'Completed', 'Cancelled']

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

// US-13: trip list, filterable by status. Fetches a fresh list every time
// reflects what "All" statuses actually contains right now.
export default function Trips() {
  const [tab, setTab] = useState('All')
  const [query, setQuery] = useState('')
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let cancelled = false

    async function loadTrips() {
      setLoading(true)
      setLoadError(null)
      try {
        const status = tab === 'All' ? undefined : tab
        const data = await tripService.listTrips(status)
        if (cancelled) return
        setTrips(data)
      } catch (err) {
        if (!cancelled) setLoadError(err.message || 'Unable to load trips.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    loadTrips()
    return () => {
      cancelled = true
    }
  }, [tab, reloadKey])

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return trips
    return trips.filter((t) =>
      t.pickupLocation.toLowerCase().includes(q) ||
      t.dropoffLocation.toLowerCase().includes(q) ||
      t.id.toLowerCase().includes(q)
    )
  }, [trips, query])

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'pickupLocation', header: 'Pickup' },
    { key: 'dropoffLocation', header: 'Drop-off' },
    { key: 'requiredCapacity', header: 'Capacity' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdAt', header: 'Created', render: (r) => formatDateTime(r.createdAt) },
  ]

  return (
    <AppShell title="Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">Trips</div>
          <p className="page-header-desc">Create trip requests and monitor them through their lifecycle.</p>
        </div>
        <div className="page-header-actions">
          <Link to="/dispatcher/trips/create">
            <Button variant="accent">+ Create Trip Request</Button>
          </Link>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-input-wrap">
          <Icon name="search" size={15} className="icon" />
          <input className="search-input" placeholder="Search by pickup, drop-off or trip ID" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
      </div>

      <div className="trip-tabs">
        {TABS.map((t) => (
          <button key={t} className={`trip-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>{t}</button>
        ))}
      </div>

      {loadError ? (
        <div className="card card-pad">
          <ErrorMessage>{loadError}</ErrorMessage>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
          </div>
        </div>
      ) : (
        <DataTable
          columns={columns}
          rows={rows}
          loading={loading}
          rowKey="id"
          emptyTitle={tab === 'All' ? 'No trips yet' : `No ${tab} trips`}
          emptyDescription={tab === 'All' ? 'Create a trip request to get started.' : 'Try a different tab or search.'}
        />
      )}
    </AppShell>
  )
}