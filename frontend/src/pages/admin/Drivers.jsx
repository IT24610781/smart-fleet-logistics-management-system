import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { MOCK_DRIVERS } from '../../utils/mockData'

// Driver *creation* (US-01) is fully wired to POST /api/users/drivers.
// Driver *listing* has no backend endpoint yet, so this page reads from
// mock data — swap useState(MOCK_DRIVERS) for userService.listDrivers()
// once GET /api/users/drivers exists; the table markup won't need to change.
export default function Drivers() {
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('all')

  const rows = useMemo(() => {
    return MOCK_DRIVERS.filter((d) => {
      const matchesQuery = !query || d.name.toLowerCase().includes(query.toLowerCase()) || d.username.toLowerCase().includes(query.toLowerCase())
      const matchesStatus = status === 'all' || d.status.toLowerCase() === status
      return matchesQuery && matchesStatus
    })
  }, [query, status])

  const columns = [
    { key: 'name', header: 'Name', render: (r) => <span className="cell-primary">{r.name}</span> },
    { key: 'username', header: 'Username', render: (r) => <span className="cell-id">{r.username}</span> },
    { key: 'email', header: 'Email' },
    { key: 'contact', header: 'Contact' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdDate', header: 'Created Date' },
    {
      key: 'actions', header: 'Actions',
      render: () => (
        <div className="cell-actions">
          <Button size="sm" variant="ghost">View</Button>
          <Button size="sm" variant="ghost">Edit</Button>
          <Button size="sm" variant="danger">Deactivate</Button>
        </div>
      ),
    },
  ]

  return (
    <AppShell title="Drivers">
      <div className="page-header">
        <div>
          <div className="page-header-title">Drivers</div>
          <p className="page-header-desc">Manage driver accounts created for the fleet. New drivers receive their credentials after account creation.</p>
        </div>
        <div className="page-header-actions">
          <Link to="/admin/drivers/create"><Button variant="accent">+ Create Driver</Button></Link>
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

      <DataTable
        columns={columns}
        rows={rows}
        emptyTitle="No drivers found"
        emptyDescription="Try a different search or create a new driver account."
      />
    </AppShell>
  )
}
