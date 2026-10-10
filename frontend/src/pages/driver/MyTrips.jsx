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

  const [locationSharing, setLocationSharing] = useState(false)
  const [locationError, setLocationError] = useState(null)

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

  const activeTripId = trips.find((t) => t.status === 'In-Progress')?.id ?? null

  useEffect(() => {
    if (!activeTripId || !window.navigator.geolocation) {
      // Nothing to start - the cleanup from the PREVIOUS run (if any) already
      // reset locationSharing/locationError, so no synchronous setState here.
      return undefined
    }

    let cancelled = false

    function sendOnce() {
      window.navigator.geolocation.getCurrentPosition(
        async (position) => {
          if (cancelled) return
          try {
            await trackingService.submitLocation({
              tripId: activeTripId,
              latitude: position.coords.latitude,
              longitude: position.coords.longitude,
              observedAt: new Date().toISOString(),
            })
            if (!cancelled) {
              setLocationSharing(true)
              setLocationError(null)
            }
          } catch (err) {
            if (!cancelled) setLocationError(err.message || 'Unable to share your location.')
          }
        },
        () => {
          if (!cancelled) setLocationError('Could not get your current location. Check your browser\u2019s location permission.')
        },
        { enableHighAccuracy: true, timeout: 10000 }
      )
    }

    sendOnce()
    const interval = window.setInterval(sendOnce, 30000)

    return () => {
      cancelled = true
      window.clearInterval(interval)
      setLocationSharing(false)
    }
  }, [activeTripId])

  // Derived at render time, not stored in state - avoids a setState call
  // inside the effect above just to report "this browser can't do it".
  const geolocationUnsupported = Boolean(activeTripId) && !window.navigator.geolocation

  async function confirmDecision() {
    if (!dialog) return
    setBusy(true)
    setRespondError(null)
    try {
      if (dialog.decision === 'complete') {
        await tripService.completeTrip(dialog.tripId)
        setDialog(null)
        setLastAction('Trip marked as completed.')
      } else {
        await tripService.respondToTrip(dialog.tripId, dialog.decision)
        setDialog(null)
        setLastAction(dialog.decision === 'accept' ? 'Trip accepted.' : 'Trip rejected and returned to Dispatch.')
      }
      setReloadKey((k) => k + 1)
    } catch (err) {
      setDialog(null)
      setRespondError(
        err.status === 409
          ? (err.message || 'This trip has already changed status, or is no longer assigned to you.')
          : err.status === 404
            ? 'This trip could not be found. It may have been reassigned.'
            : err.message || 'Something went wrong. Please try again.'
      )
    } finally {
      setBusy(false)
    }
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
        <Button size="sm" variant="accent" onClick={() => setDialog({ tripId: row.id, decision: 'complete' })}>
          Mark Completed
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
    { key: 'updatedAt', header: 'Updated', render: (r) => formatDateTime(r.updatedAt) },
    { key: 'action', header: '', render: actionsFor },
  ]

  return (
    <AppShell title="My Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">My Trips</div>
          <p className="page-header-desc">Trips assigned to you. You can accept or reject new assignments and track your history.</p>
        </div>
      </div>

      <SuccessMessage>{lastAction}</SuccessMessage>
      <ErrorMessage>{respondError}</ErrorMessage>
      {locationSharing && (
        <div className="flex items-center gap-1.5 font-body-sm text-body-sm text-on-surface-variant" style={{ marginTop: 'var(--space-2)' }}>
          <span aria-hidden="true" style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--positive, #2e7d32)', display: 'inline-block' }} />
          Sharing your location automatically for your active trip.
        </div>
      )}
      <ErrorMessage>{geolocationUnsupported ? 'Your browser does not support location services, so location can’t be shared automatically.' : locationError}</ErrorMessage>

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
        title={
          dialog?.decision === 'accept' ? 'Accept this trip?'
            : dialog?.decision === 'complete' ? 'Mark this trip as completed?'
              : 'Reject this trip?'
        }
        body={
          dialog?.decision === 'accept' ? 'You will be responsible for completing this trip.'
            : dialog?.decision === 'complete' ? 'This closes out the trip and frees your vehicle for new assignments. Automatic location sharing will stop.'
              : 'This trip will be returned to Dispatch and its vehicle freed up.'
        }
        confirmLabel={
          dialog?.decision === 'accept' ? 'Accept'
            : dialog?.decision === 'complete' ? 'Mark Completed'
              : 'Reject'
        }
        cancelLabel="Cancel"
        variant={dialog?.decision === 'reject' ? 'danger' : 'accent'}
        loading={busy}
        onConfirm={confirmDecision}
        onCancel={() => setDialog(null)}
      />
    </AppShell>
  )
}