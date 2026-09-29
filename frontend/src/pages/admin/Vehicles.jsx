import PageHeading from '../../components/common/PageHeading'
import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import AppShell from '../../components/layout/AdminShell'
import DataTable from '../../components/common/AdminTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { listVehicles, deleteVehicle } from '../../services/vehicleService'

// US-05: View Vehicle List
// US-07: Admin removes a vehicle from the registry

export default function Vehicles() {
  const [params, setParams] = useSearchParams()
  const query = params.get('q') || ''
  const status = params.get('status') || 'all'
  const setFilter = (key,value) => setParams(prev => { const next = new window.URLSearchParams(prev); if (value && value !== 'all') next.set(key,value); else next.delete(key); return next }, { replace:true })
  const setQuery = value => setFilter('q',value)
  const setStatus = value => setFilter('status',value)
  const [reload,setReload] = useState(0)
  const [vehicles, setVehicles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState('')

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setError('')
      try {
        const data = await listVehicles()
        if (!cancelled) setVehicles(data)
      } catch (err) {
        if (!cancelled) setError(err.message || 'Could not load vehicles.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => { cancelled = true }
  }, [reload])

  const rows = useMemo(() => {
    return vehicles.filter((v) => {
      const q = query.toLowerCase()
      const matchesQuery = !q || v.type.toLowerCase().includes(q) || v.registrationNo.toLowerCase().includes(q)
      const matchesStatus = status === 'all' || v.status.toLowerCase() === status
      return matchesQuery && matchesStatus
    })
  }, [query, status, vehicles])

  function handleDeleteClick(vehicle) {
    setDeleteError('')
    setDeleteTarget(vehicle)
  }

  function handleCancelDelete() {
    if (deleting) return
    setDeleteTarget(null)
    setDeleteError('')
  }

  async function handleConfirmDelete() {
    if (!deleteTarget) return
    setDeleting(true)
    setDeleteError('')
    try {
      await deleteVehicle(deleteTarget.id)
      setVehicles((prev) => prev.filter((v) => v.id !== deleteTarget.id))
      setDeleteTarget(null)
    } catch (err) {
      const message =
        err.response?.data?.message ||
        err.message ||
        'Could not remove vehicle.'
      setDeleteError(message)
    } finally {
      setDeleting(false)
    }
  }

  const columns = [
    { key: 'registrationNo', header: 'Registration Number', render: (r) => <span className="cell-primary">{r.registrationNo}</span> },
    { key: 'type', header: 'Type' },
    { key: 'capacity', header: 'Capacity' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    {
      key: 'actions',
      header: 'Actions',
      align: 'right',
      render: (r) => (
        <div className="flex items-center gap-1 justify-end">
          <Link className="admin-cancel-link" to={`/admin/vehicles/${r.id}/edit`} state={{ vehicle: r }} aria-label={`Edit ${r.registrationNo}`}>
                <Icon name="edit" size={15} className="icon" />
            </Link>
            <Button
            variant="ghost"
            type="button"
            aria-label={`Delete ${r.registrationNo}`}
            onClick={() => handleDeleteClick(r)}
      >
                <Icon name="trash" size={15} className="icon" />
          </Button>
        </div>
        ),
    },
  ]

  return (
    <AppShell title="Vehicles">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Fleet management" title="Vehicles" />
          <p className="page-header-desc">Fleet inventory and current status, live from the Fleet Service.</p>
        </div>
        <div className="page-header-actions">
          <Link className="admin-cancel-link" to="/admin/vehicles/create">Add Vehicle</Link>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-input-wrap">
          <Icon name="search" size={15} className="icon" />
          <input className="search-input" placeholder="Search by registration number or type" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
        <select className="select-input" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="all">All statuses</option>
          <option value="available">Available</option>
          <option value="on trip">On Trip</option>
          <option value="under maintenance">Under Maintenance</option>
        </select>
      </div>

      {loading && <p className="page-status-text">Loading vehicles…</p>}

      {!loading && error && (
        <div role="alert"><p className="page-status-text page-status-error">{error}</p><Button variant="secondary" onClick={() => setReload(x => x+1)}>Retry</Button></div>
      )}

      {!loading && !error && vehicles.length === 0 && (
        <DataTable columns={columns} rows={[]} emptyTitle="No vehicles registered yet" emptyDescription="Add your first vehicle to get started." />
      )}

      {!loading && !error && vehicles.length > 0 && (
        <DataTable columns={columns} rows={rows} emptyTitle="No vehicles found" emptyDescription="Try a different search or filter." />
      )}

        <ConfirmDialog
        open={!!deleteTarget}
        title="Remove vehicle?"
        body={deleteTarget ? `This will permanently remove ${deleteTarget.registrationNo} from the registry.` : ''}
        errorMessage={deleteError || undefined}
        confirmLabel="Remove"
        cancelLabel="Cancel"
        variant="danger"
        loading={deleting}
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
      />
    </AppShell>
  )
}