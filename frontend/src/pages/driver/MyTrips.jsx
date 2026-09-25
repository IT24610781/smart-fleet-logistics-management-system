import { useEffect, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

//US-12 a driver's own trips, wired to the real Trip Dispatch Service.
// Accept/reject is only offered while a trip is Assigned; anything else is
// read-only history.
export default function MyTrips() {
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [dialog, setDialog] = useState(null)
  const [busy, setBusy] = useState(false)
  const [respondError, setRespondError] = useState(null)
  const [lastAction, setLastAction] = useState(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getMyTrips()
        if (cancelled) return
        setTrips(data)
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

  function actionsFor(row) {
    if (row.status === 'Assigned') {
      return (
        <div className="cell-actions">
          <Button size="sm" variant="secondary" onClick={() => setDialog({ tripId: row.id, decision: 'accept' })}>Accept</Button>
          <Button size="sm" variant="danger" onClick={() => setDialog({ tripId: row.id, decision: 'reject' })}>Reject</Button>
        </div>
      )
    }
    return null
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id.slice(0, 8)}</span> },
    { key: 'pickupLocation', header: 'Pickup' },
    { key: 'dropoffLocation', header: 'Drop-off' },
    { key: 'requiredCapacity', header: 'Capacity' },
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