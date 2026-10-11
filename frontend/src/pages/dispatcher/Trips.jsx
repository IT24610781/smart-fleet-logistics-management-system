import PageHeading from '../../components/common/PageHeading'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import { useToast } from '../../context/ToastContext'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'
import './Trips.css'

const CANCELLABLE_STATUSES = ['Requested', 'Assigned', 'In-Progress']
const CANCEL_DISABLED_STATUSES = ['In-Progress']

const TABS = ['All', 'Requested', 'Assigned', 'In-Progress', 'Completed', 'Cancelled']

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

function driverName(driver) {
  if (!driver) return null
  const name = [driver.firstName, driver.lastName].filter(Boolean).join(' ')
  return name || driver.username
}

// US-13: trip list, filterable by status. Fetches a fresh list every time
// the tab changes, and joins against the driver list so the Dispatcher can
// see who a trip is with and, once accepted, how to reach them.
export default function Trips() {
  const { showToast } = useToast()
  const [tab, setTab] = useState('All')
  const [query, setQuery] = useState('')
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [driversById, setDriversById] = useState({})

  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState(null)

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

  useEffect(() => {
    let cancelled = false

    async function loadDrivers() {
      try {
        const data = await userService.listDrivers()
        if (cancelled) return
        setDriversById(Object.fromEntries(data.map((d) => [d.id, d])))
      } catch {
        // Non-fatal: the trip list still works, just without driver names.
      }
    }

    loadDrivers()
    return () => {
      cancelled = true
    }
  }, [])

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return trips
    return trips.filter((t) =>
      t.pickupLocation.toLowerCase().includes(q) ||
      t.dropoffLocation.toLowerCase().includes(q) ||
      t.id.toLowerCase().includes(q)
    )
  }, [trips, query])

  function driverCell(row) {
    if (row.status === 'Requested' && row.rejectedByDriverId) {
      const name = driverName(driversById[row.rejectedByDriverId]) || 'The previously assigned driver'
      return (
        <div className="cell-warning">
          <Icon name="alert" size={14} />
          <span>{name} is busy — assign someone else</span>
        </div>
      )
    }

    if (row.assignedDriverId) {
      const driver = driversById[row.assignedDriverId]
      const name = driverName(driver) || 'Assigned driver'
      // "awaiting response" only makes sense while the trip is sitting with
      // the Driver, unactioned (status === 'Assigned'). Completed/Cancelled
      // trips keep their assignedDriverId as a historical record - without
      // this check they'd wrongly say "awaiting response" forever, since
      // that branch used to catch every non-In-Progress status.
      if (row.status === 'Assigned') {
        return <span>{name} · awaiting response</span>
      }
      return (
        <div>
          <div>{name}</div>
          {driver?.contactNumber && (
            <div className="page-header-desc">{driver.contactNumber}</div>
          )}
        </div>
      )
    }

    return null
  }

  function handleCancelClick(trip) {
    setCancelError(null)
    setCancelTarget(trip)
  }

  function handleCancelDialogClose() {
    if (cancelling) return
    setCancelTarget(null)
    setCancelError(null)
  }

  async function handleConfirmCancel() {
    if (!cancelTarget) return
    setCancelling(true)
    setCancelError(null)
    try {
      await tripService.cancelTrip(cancelTarget.id)
      setTrips((prev) => prev.map((t) => (t.id === cancelTarget.id ? { ...t, status: 'Cancelled' } : t)))
      setCancelTarget(null)
      showToast('Trip cancelled.', 'success')
    } catch (err) {
      setCancelError(
        err.status === 409
          ? (err.message || 'This trip can no longer be cancelled - it may have changed status.')
          : err.message || 'Unable to cancel this trip. Please try again.'
      )
    } finally {
      setCancelling(false)
    }
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'pickupLocation', header: 'Pickup' },
    { key: 'dropoffLocation', header: 'Drop-off' },
    { key: 'requiredCapacity', header: 'Capacity' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'driver', header: 'Driver', render: driverCell },
    { key: 'createdAt', header: 'Created', render: (r) => formatDateTime(r.createdAt) },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (r) => (
        <div className="cell-actions">
          {CANCELLABLE_STATUSES.includes(r.status) && (
            <Button
              variant="ghost"
              className="btn-glass-danger"
              disabled={CANCEL_DISABLED_STATUSES.includes(r.status)}
              title={CANCEL_DISABLED_STATUSES.includes(r.status) ? 'Cancel is disabled once the driver has accepted the trip' : undefined}
              onClick={() => handleCancelClick(r)}
            >
              Cancel
            </Button>
          )}
          <Link to={`/dispatcher/trips/${r.id}/details`}>
            <Button variant="ghost" className="btn-glass-info">View details</Button>
          </Link>
        </div>
      ),
    },
  ]

  return (
    <AppShell title="Trips">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Dispatch operations" title="Trips" />
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

      <ConfirmDialog
        open={!!cancelTarget}
        title="Cancel this trip?"
        body={
          cancelTarget
            ? `Cancel the trip from ${cancelTarget.pickupLocation} to ${cancelTarget.dropoffLocation}? ${
                cancelTarget.status === 'Assigned'
                  ? 'The assigned vehicle will be freed up for new assignments.'
                  : 'This cannot be undone.'
              }`
            : ''
        }
        confirmLabel="Cancel trip"
        cancelLabel="Keep trip"
        variant="danger"
        loading={cancelling}
        errorMessage={cancelError || undefined}
        onConfirm={handleConfirmCancel}
        onCancel={handleCancelDialogClose}
      />
    </AppShell>
  )
}