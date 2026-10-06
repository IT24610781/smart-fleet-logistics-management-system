import { useEffect, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'
import * as trackingService from '../../services/trackingService'

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
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

// US-12: a driver's own trips, wired to the real Trip Dispatch Service.
// Accept/reject is only offered while a trip is Assigned; anything else is
// read-only history. Once a trip is active, the Dispatcher's contact details
// are resolved and shown so the Driver knows who to call.
export default function MyTrips() {
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [dispatchersById, setDispatchersById] = useState({})

  const [dialog, setDialog] = useState(null)
  const [busy, setBusy] = useState(false)
  const [respondError, setRespondError] = useState(null)
  const [lastAction, setLastAction] = useState(null)

  const [sendingLocationFor, setSendingLocationFor] = useState(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getMyTrips()
        if (cancelled) return
        setTrips(data)

        // Resolve each distinct Dispatcher referenced by these trips, once -
        // a lookup-by-id endpoint, not a bulk list, so dedupe first.
        const dispatcherIds = [...new Set(data.map((t) => t.createdByDispatcherId).filter(Boolean))]
        const results = await Promise.all(
          dispatcherIds.map((id) => userService.getUser(id).catch(() => null))
        )
        if (!cancelled) {
          setDispatchersById(Object.fromEntries(
            dispatcherIds.map((id, i) => [id, results[i]]).filter(([, u]) => u)
          ))
        }
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

  async function confirmDecision() {
    if (!dialog) return
    setBusy(true)
    setRespondError(null)
    try {
      await tripService.respondToTrip(dialog.tripId, dialog.decision)
      setDialog(null)
      setLastAction(dialog.decision === 'accept' ? 'Trip accepted.' : 'Trip rejected and returned to Dispatch.')
      setReloadKey((k) => k + 1)
    } catch (err) {
      setDialog(null)
      setRespondError(
        err.status === 409
          ? (err.message || 'This trip has already been responded to, or is no longer assigned to you.')
          : err.status === 404
            ? 'This trip could not be found. It may have been reassigned.'
            : err.message || 'Something went wrong. Please try again.'
      )
    } finally {
      setBusy(false)
    }
  }

  async function handleSendLocation(tripId) {
    if (!window.navigator.geolocation) {
      setRespondError('Your browser does not support location services.')
      return
    }
    setSendingLocationFor(tripId)
    setRespondError(null)
    window.navigator.geolocation.getCurrentPosition(
      async (position) => {
        try {
          await trackingService.submitLocation({
            tripId,
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            observedAt: new Date().toISOString(),
          })
          setLastAction('Location sent.')
        } catch (err) {
          setRespondError(err.message || 'Unable to send your location. Please try again.')
        } finally {
          setSendingLocationFor(null)
        }
      },
      () => {
        setRespondError('Could not get your current location. Check your browser\u2019s location permission.')
        setSendingLocationFor(null)
      },
      { enableHighAccuracy: true, timeout: 10000 }
    )
  }

  function actionsFor(row) {
    if (row.status === 'Assigned') {
      return (
        <div className="cell-actions">
          <Button size="sm" variant="secondary" onClick={() => setDialog({ tripId: row.id, decision: 'accept' })}>Accept</Button>
          <Button size="sm" variant="danger" onClick={() => setDialog({ tripId: row.id, decision: 'reject' })}>Reject</Button>
        </div>
      )
    }
    if (row.status === 'In-Progress') {
      return (
        <Button
          size="sm"
          variant="secondary"
          loading={sendingLocationFor === row.id}
          onClick={() => handleSendLocation(row.id)}
        >
          Send location
        </Button>
      )
    }
    return null
  }

  function dispatcherCell(row) {
    if (!row.createdByDispatcherId) return <span className="page-header-desc">—</span>
    const dispatcher = dispatchersById[row.createdByDispatcherId]
    if (!dispatcher) return <span className="page-header-desc">—</span>
    return (
      <div>
        <div>{userLabel(dispatcher)}</div>
        {dispatcher.contactNumber && <div className="page-header-desc">{dispatcher.contactNumber}</div>}
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
    { key: 'updatedAt', header: 'Updated', render: (r) => formatDateTime(r.updatedAt) },
    { key: 'action', header: '', render: actionsFor },
  ]

  return (
    <AppShell title="My Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">My Trips</div>
          <p className="page-header-desc">Trips assigned to you — accept or reject new assignments and track your history.</p>
        </div>
      </div>

      <SuccessMessage>{lastAction}</SuccessMessage>
      <ErrorMessage>{respondError}</ErrorMessage>

      {loadError ? (
        <div className="card card-pad" style={{ marginTop: 'var(--space-3)' }}>
          <ErrorMessage>{loadError}</ErrorMessage>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
          </div>
        </div>
      ) : (
        <div style={{ marginTop: (lastAction || respondError) ? 'var(--space-3)' : 0 }}>
          <DataTable
            columns={columns}
            rows={trips}
            loading={loading}
            rowKey="id"
            emptyTitle="No trips assigned yet"
            emptyDescription="New trip assignments will appear here."
          />
        </div>
      )}

      <ConfirmDialog
        open={!!dialog}
        title={dialog?.decision === 'accept' ? 'Accept this trip?' : 'Reject this trip?'}
        body={dialog?.decision === 'accept' ? 'You will be responsible for completing this trip.' : 'This trip will be returned to Dispatch and its vehicle freed up.'}
        confirmLabel={dialog?.decision === 'accept' ? 'Accept' : 'Reject'}
        cancelLabel="Cancel"
        variant={dialog?.decision === 'accept' ? 'accent' : 'danger'}
        loading={busy}
        onConfirm={confirmDecision}
        onCancel={() => setDialog(null)}
      />
    </AppShell>
  )
}