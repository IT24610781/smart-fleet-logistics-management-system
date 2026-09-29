import PageHeading from '../../components/common/PageHeading'
import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import AppShell from '../../components/layout/AdminShell'
import DataTable from '../../components/common/AdminTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import AdminDetails from '../../components/common/AdminDetails'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { useToast } from '../../context/ToastContext'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import * as userService from '../../services/userService'

export default function Drivers() {
  const [params, setParams] = useSearchParams()
  const query = params.get('q') || ''
  const status = params.get('status') || 'all'
  const setFilter = (key, value) => setParams(prev => { const next = new window.URLSearchParams(prev); if (value && value !== 'all') next.set(key,value); else next.delete(key); return next }, { replace:true })
  const setQuery = value => setFilter('q',value)
  const setStatus = value => setFilter('status',value)
  const { showToast } = useToast()
  const [deactivateTarget, setDeactivateTarget] = useState(null)
  const [deactivating, setDeactivating] = useState(false)
  const [deactivationError, setDeactivationError] = useState(null)
  const [selected, setSelected] = useState(null)
  const [reload, setReload] = useState(0)
  const [drivers, setDrivers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    async function loadDrivers() {
      setLoading(true)
      setError(null)
      try {
        const data = await userService.listDrivers()
        setDrivers(data.map((driver) => ({
          id: driver.id,
          name: `${driver.firstName} ${driver.lastName}`.trim(),
          username: driver.username,
          email: driver.email,
          contact: driver.contactNumber,
          status: driver.isActive ? 'Active' : 'Inactive',
          createdDate: new Date(driver.createdAt).toISOString().slice(0, 10),
        })))
      } catch (err) {
        setError(err.message || 'Unable to load drivers.')
      } finally {
        setLoading(false)
      }
    }

    loadDrivers()
  }, [reload])

  const rows = useMemo(() => {
    return drivers.filter((d) => {
      const matchesQuery = !query || d.name.toLowerCase().includes(query.toLowerCase()) || d.username.toLowerCase().includes(query.toLowerCase())
      const matchesStatus = status === 'all' || d.status.toLowerCase() === status
      return matchesQuery && matchesStatus
    })
  }, [drivers, query, status])

  async function confirmDeactivation() {
    if (!deactivateTarget || deactivating) return
    setDeactivating(true)
    setDeactivationError(null)
    try {
      await userService.deactivateDriver(deactivateTarget.id)
      setDrivers(current => current.map(driver => driver.id === deactivateTarget.id ? { ...driver, status: 'Inactive' } : driver))
      setSelected(current => current?.id === deactivateTarget.id ? { ...current, status: 'Inactive' } : current)
      setDeactivateTarget(null)
      showToast('Driver account deactivated.', 'success')
    } catch (err) {
      setDeactivationError(err.message || 'Unable to deactivate the Driver.')
    } finally {
      setDeactivating(false)
    }
  }

  const columns = [
    { key: 'name', header: 'Name', render: (r) => <span className="cell-primary">{r.name}</span> },
    { key: 'username', header: 'Username', render: (r) => <span className="cell-id">{r.username}</span> },
    { key: 'email', header: 'Email' },
    { key: 'contact', header: 'Contact' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdDate', header: 'Created Date' },
    {
      key: 'actions', header: 'Actions',
      render: (driver) => (
        <div className="cell-actions">
          <Button size="sm" variant="ghost" onClick={() => setSelected(driver)}>View</Button>
          <Button size="sm" variant="ghost" disabled title="Driver editing is not available yet">Edit</Button>
          <Button size="sm" variant="secondary" disabled={driver.status === 'Inactive' || deactivating} onClick={() => { setDeactivationError(null); setDeactivateTarget(driver) }}>Deactivate</Button>
        </div>
      ),
    },
  ]

  return (
    <AppShell title="Drivers">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="People management" title="Drivers" />
          <p className="page-header-desc">Manage driver accounts created for the fleet. New drivers receive their credentials after account creation.</p>
        </div>
        <div className="page-header-actions">
          <Link className="admin-cancel-link" to="/admin/drivers/create">Create Driver</Link>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-input-wrap">
          <Icon name="search" size={15} className="icon" />
          <input className="search-input" placeholder="Search by name or username" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
        <select className="select-input" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="all">All statuses</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </select>
      </div>

      <p className="admin-muted">Inactive Drivers cannot log in or receive new assignments. Driver editing is not available yet.</p>
      <ErrorMessage>{error}</ErrorMessage>
      {error && <Button variant="secondary" onClick={() => setReload(x => x+1)}>Retry</Button>}

      {!error && <DataTable
        columns={columns}
        rows={rows}
        loading={loading}
        emptyTitle="No drivers found"
        emptyDescription="Try a different search or create a new driver account."
      />}
      <ConfirmDialog open={!!deactivateTarget} title="Deactivate Driver?" body={`Deactivate ${deactivateTarget?.name}? Login and existing sessions will be blocked. The account and trip history will be kept. Assigned or active trips must be resolved first.`} confirmLabel="Deactivate" loading={deactivating} errorMessage={deactivationError} onConfirm={confirmDeactivation} onCancel={() => { if (!deactivating) setDeactivateTarget(null) }} />
      {selected && <AdminDetails title="Driver details" onClose={() => setSelected(null)} fields={[['Name',selected.name],['Username',selected.username],['Email',selected.email],['Contact',selected.contact],['Status',selected.status],['Created',selected.createdDate]]}/>}
    </AppShell>
  )
}
