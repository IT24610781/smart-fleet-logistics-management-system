import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { listVehicles } from '../../services/vehicleService'

// US-05: View Vehicle List 

export default function Vehicles() {
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('all')
  const [vehicles, setVehicles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

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
  }, [])

  const rows = useMemo(() => {
    return vehicles.filter((v) => {
      const q = query.toLowerCase()
      const matchesQuery = !q || v.type.toLowerCase().includes(q) || v.registrationNo.toLowerCase().includes(q)
      const matchesStatus = status === 'all' || v.status.toLowerCase() === status
      return matchesQuery && matchesStatus
    })
  }, [query, status, vehicles])

  const columns = [
    { key: 'registrationNo', header: 'Registration Number', render: (r) => <span className="cell-primary">{r.registrationNo}</span> },
    { key: 'type', header: 'Type' },
    { key: 'capacity', header: 'Capacity' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
  ]

  return (
    <AppShell title="Vehicles">
      <div className="page-header">
        <div>
          <div className="page-header-title">Vehicles</div>
          <p className="page-header-desc">Fleet inventory and current status, live from the Fleet Service.</p>
        </div>
        <div className="page-header-actions">
          <Link to="/admin/vehicles/create"><Button variant="accent">+ Add Vehicle</Button></Link>
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
        <p className="page-status-text page-status-error">{error}</p>
      )}

      {!loading && !error && vehicles.length === 0 && (
        <DataTable columns={columns} rows={[]} emptyTitle="No vehicles registered yet" emptyDescription="Add your first vehicle to get started." />
      )}

      {!loading && !error && vehicles.length > 0 && (
        <DataTable columns={columns} rows={rows} emptyTitle="No vehicles found" emptyDescription="Try a different search or filter." />
      )}
    </AppShell>
  )
}