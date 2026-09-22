import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import * as tripService from '../../services/tripService'
import { MOCK_TRIPS } from '../../utils/mockData'
import './Trips.css'

const TABS = ['All', 'Pending', 'Assigned', 'In Progress', 'Completed', 'Rejected']

export default function Trips() {
  const [tab, setTab] = useState('All')
  const [query, setQuery] = useState('')

  const rows = useMemo(() => {
    const byTab = tab === 'All' ? MOCK_TRIPS : MOCK_TRIPS.filter((t) => t.status === tab)
    const q = query.toLowerCase()
    if (!q) return byTab
    return byTab.filter((t) =>
      t.origin.toLowerCase().includes(q) ||
      t.destination.toLowerCase().includes(q) ||
      t.driver.toLowerCase().includes(q) ||
      t.vehicle.toLowerCase().includes(q) ||
      t.id.toLowerCase().includes(q)
    )
  }, [tab, query])

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'origin', header: 'Origin' },
    { key: 'destination', header: 'Destination' },
    { key: 'vehicle', header: 'Vehicle' },
    { key: 'driver', header: 'Driver' },
    { key: 'scheduledTime', header: 'Scheduled Time' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdDate', header: 'Created Date' },
  ]

  return (
    <AppShell title="Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">Trips</div>
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
          <input className="search-input" placeholder="Search by route, driver or vehicle" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
      </div>

      <div className="trip-tabs">
        {TABS.map((t) => (
          <button key={t} className={`trip-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>{t}</button>
        ))}
      </div>

      <DataTable columns={columns} rows={rows} emptyTitle="No trips match your search" />
    </AppShell>
  )
}