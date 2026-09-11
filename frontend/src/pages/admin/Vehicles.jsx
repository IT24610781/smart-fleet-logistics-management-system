import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { MOCK_VEHICLES } from '../../utils/mockData'

// FR3/FR5 — vehicle registration and status updates. The Vehicle Service
// isn't live yet, so this reads mock data; vehicleService.js already
// exposes listVehicles()/updateVehicleStatus() for when it is.
export default function Vehicles() {
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('all')

  const rows = useMemo(() => {
    return MOCK_VEHICLES.filter((v) => {
      const q = query.toLowerCase()
      const matchesQuery = !q || v.model.toLowerCase().includes(q) || v.regNumber.toLowerCase().includes(q) || v.id.toLowerCase().includes(q)
      const matchesStatus = status === 'all' || v.status.toLowerCase() === status
      return matchesQuery && matchesStatus
    })
  }, [query, status])

  const columns = [
    { key: 'id', header: 'Vehicle ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'regNumber', header: 'Registration Number', render: (r) => <span className="cell-primary">{r.regNumber}</span> },
    { key: 'type', header: 'Type' },
    { key: 'model', header: 'Model' },
    { key: 'capacity', header: 'Capacity' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'currentDriver', header: 'Current Driver' },
  ]

  return (
    <AppShell title="Vehicles">
      <div className="page-header">
        <div>
          <div className="page-header-title">Vehicles</div>
          <p className="page-header-desc">Fleet inventory and current status. Vehicle creation is connected to the live Fleet Service — this list still shows demo data until GET /api/vehicles is implemented.</p>
        </div>
        <div className="page-header-actions">
          <Link to="/admin/vehicles/create"><Button variant="accent">+ Add Vehicle</Button></Link>
        </div>
      </div>

      <div className="toolbar">
        <div className="search-input-wrap">
          <Icon name="search" size={15} className="icon" />
          <input className="search-input" placeholder="Search by ID, model or plate" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
        <select className="select-input" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="all">All statuses</option>
          <option value="available">Available</option>
          <option value="in use">In Use</option>
          <option value="maintenance">Maintenance</option>
          <option value="inactive">Inactive</option>
        </select>
      </div>

      <DataTable columns={columns} rows={rows} emptyTitle="No vehicles found" emptyDescription="Try a different search or filter." />
    </AppShell>
  )
}