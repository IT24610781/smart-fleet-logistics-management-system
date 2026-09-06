import { useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'
import { MOCK_DRIVER_TRIPS } from '../../utils/mockData'

// FR8/FR9 — accept/reject and status updates are wired to tripService, a
// placeholder until the Trip Service exists. Only actions valid for the
// trip's current status are shown, per section 20.
export default function MyTrips() {
  const [dialog, setDialog] = useState(null) // { tripId, decision }
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)

  async function confirmDecision() {
    setBusy(true)
    setError(null)
    try {
      await tripService.respondToTrip(dialog.tripId, dialog.decision)
      setDialog(null)
    } catch (err) {
      setError(err.status === 404
        ? 'Trip responses aren\u2019t connected to the backend yet.'
        : (err.message || 'Something went wrong. Please try again.'))
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
    if (row.status === 'In Progress') {
      return <Button size="sm" variant="secondary">Update Status</Button>
    }
    return <Button size="sm" variant="ghost">View Details</Button>
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'origin', header: 'Origin' },
    { key: 'destination', header: 'Destination' },
    { key: 'vehicle', header: 'Vehicle' },
    { key: 'date', header: 'Date' },
    { key: 'scheduledTime', header: 'Schedule' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'action', header: 'Action', render: actionsFor },
  ]

  return (
    <AppShell title="My Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">My Trips</div>
          <p className="page-header-desc">Trips assigned to you and only you — accept or reject new assignments and track your history.</p>
        </div>
      </div>

      <ErrorMessage>{error}</ErrorMessage>
      <div style={{ marginTop: error ? 'var(--space-3)' : 0 }}>
        <DataTable columns={columns} rows={MOCK_DRIVER_TRIPS} emptyTitle="No trips assigned yet" />
      </div>

      <ConfirmDialog
        open={!!dialog}
        title={dialog?.decision === 'accept' ? 'Accept this trip?' : 'Reject this trip?'}
        body={dialog?.decision === 'accept' ? 'You will be assigned as the driver for this trip.' : 'This trip will be returned to the dispatcher for reassignment.'}
        confirmLabel={dialog?.decision === 'accept' ? 'Accept' : 'Reject'}
        variant={dialog?.decision === 'accept' ? 'primary' : 'danger'}
        loading={busy}
        onConfirm={confirmDecision}
        onCancel={() => setDialog(null)}
      />
    </AppShell>
  )
}
