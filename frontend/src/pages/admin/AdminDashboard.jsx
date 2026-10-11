import PageHeading from '../../components/common/PageHeading'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AdminShell'
import StatusBadge from '../../components/common/StatusBadge'
import DataTable from '../../components/common/DataTable'
import Icon from '../../components/common/Icon'
import AdminDetails from '../../components/common/AdminDetails'
import { useAuth } from '../../context/AuthContext'
import { listDrivers } from '../../services/userService'
import { listVehicles } from '../../services/vehicleService'
import { listTrips } from '../../services/tripService'

const date = (value) => value && !Number.isNaN(new Date(value).getTime()) ? new Date(value).toLocaleString() : 'Unavailable'
export default function AdminDashboard() {
  const { user } = useAuth()
  const [sources, setSources] = useState({ drivers: null, vehicles: null, trips: null })
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(true)
  const [selected, setSelected] = useState(null)
  useEffect(() => {
    let cancelled = false
    Promise.allSettled([listDrivers(), listVehicles(), listTrips()]).then(results => {
      if (cancelled) return
      const data = {}, failures = {}
      results.forEach((result, i) => {
        const key = ['drivers', 'vehicles', 'trips'][i]
        data[key] = result.status === 'fulfilled' ? result.value : null
        if (result.status === 'rejected') failures[key] = result.reason?.message || 'Unable to retrieve data.'
      })
      setSources(data); setErrors(failures); setLoading(false)
    })
    return () => { cancelled = true }
  }, [])
  const { drivers, vehicles, trips } = sources
  const driverName = id => drivers?.find(d => d.id === id)
  const vehicleName = id => vehicles?.find(v => v.id === id)?.registrationNo || id || 'Not assigned'
  const name = id => { const d = driverName(id); return d ? `${d.firstName} ${d.lastName}` : id || 'Not assigned' }
  const recent = [...(trips || [])].sort((a,b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0,5)
  const metrics = [
    ['Total Drivers', drivers?.length, 'users', '/admin/drivers', 'Registered Driver accounts'],
    ['Total Vehicles', vehicles?.length, 'truck', '/admin/vehicles', 'Vehicles in your registry'],
    ['Available Vehicles', vehicles?.filter(v => v.status === 'Available').length, 'grid', '/admin/vehicles?status=available', 'Ready for assignment'],
    ['Active Trips', trips?.filter(t => t.status === 'In-Progress').length, 'route', null, 'Trips currently in progress'],
  ]
  const columns = [
    { key:'id', header:'Trip', render:r => <button className="admin-text-button" onClick={() => setSelected(r)} aria-label={`View trip ${r.id}`}>{r.id.slice(0,8)}</button> },
    { key:'route', header:'Route', render:r => <div><strong>{r.pickupLocation}</strong><span className="admin-route-destination">To {r.dropoffLocation}</span></div> },
    { key:'assignedDriverId', header:'Driver', render:r => name(r.assignedDriverId) },
    { key:'assignedVehicleId', header:'Vehicle', render:r => vehicleName(r.assignedVehicleId) },
    { key:'status', header:'Status', render:r => <StatusBadge status={r.status} /> },
    { key:'createdAt', header:'Created', render:r => date(r.createdAt) },
  ]
  return <AppShell title="Fleet Administration">
    <section className="admin-hero">
      <PageHeading eyebrow="Fleet operations" title="Fleet administration" description={<> Welcome back{user?.firstName ? `, ${user.firstName}` : ''}. Manage your people, vehicles and active operations. </>} />

    </section>
    {!loading && Object.keys(errors).length > 0 && <div className="admin-source-error" role="alert"><strong>Some fleet data is unavailable.</strong>{Object.entries(errors).map(([key,error]) => <p key={key}>{key}: {error}</p>)}<p>Available sections remain visible. Reload this page to try again.</p></div>}
    <div className="admin-metrics">{metrics.map(([label,value,icon,to,hint]) => {
      const content = <><div className={`admin-metric-icon admin-metric-icon-${icon}`} aria-hidden="true"><Icon name={icon} size={46}/></div><div className="admin-metric-copy"><span className="admin-metric-label">{label}</span><strong className="admin-metric-value">{loading ? '...' : value ?? '--'}</strong><span className="admin-muted">{loading ? 'Loading' : value == null ? 'Data unavailable' : hint}</span></div></>
      return to ? <Link key={label} className="admin-metric" to={to}>{content}</Link> : <div key={label} className="admin-metric">{content}</div>
    })}</div>
    <div className="admin-dashboard-grid">
      <section className="admin-panel"><div className="admin-section-head"><div><h3>Recent trips</h3><p>The five most recently created trips</p></div></div>
        {errors.trips && !loading ? <p className="admin-muted">Trip information is unavailable. Reload this page to retry.</p> : <DataTable columns={columns} rows={recent} loading={loading} emptyTitle="No trips yet" emptyDescription="Trips will appear here when Dispatchers create them." />}
      </section>
      <aside className="admin-panel"><h3>Quick actions</h3><p className="admin-muted">Keep your fleet ready for the next trip.</p><div className="admin-quick-links">
        <Link to="/admin/drivers"><Icon name="users" size={20}/><span>Manage Drivers<small>View and manage Driver accounts</small></span><Icon name="chevronRight" size={16}/></Link>
        <Link to="/admin/vehicles/create"><Icon name="truck" size={20}/><span>Add Vehicle<small>Expand the vehicle registry</small></span><Icon name="chevronRight" size={16}/></Link>
        <Link to="/admin/reports"><Icon name="chart" size={20}/><span>Reports<small>View the reporting page</small></span><Icon name="chevronRight" size={16}/></Link>
      </div></aside>
      <section className="admin-panel"><div className="admin-section-head"><h3>Fleet availability</h3><Link to="/admin/vehicles">Manage vehicles</Link></div>
        {loading || !vehicles ? <p className="admin-muted">{loading ? 'Loading vehicles...' : 'Vehicle data unavailable.'}</p> : <div className="admin-overview">{['Available','On Trip','Under Maintenance'].map(status => <Link key={status} to={`/admin/vehicles?status=${encodeURIComponent(status.toLowerCase())}`}><StatusBadge status={status}/><strong>{vehicles.filter(v => v.status === status).length}</strong></Link>)}</div>}
      </section>
      <section className="admin-panel"><div className="admin-section-head"><h3>Driver accounts</h3><Link to="/admin/drivers">Manage Drivers</Link></div>
        {loading || !drivers ? <p className="admin-muted">{loading ? 'Loading Drivers...' : 'Driver data unavailable.'}</p> : <div className="admin-overview">{['Active','Inactive'].map(status => <Link key={status} to={`/admin/drivers?status=${status.toLowerCase()}`}><StatusBadge status={status}/><strong>{drivers.filter(d => d.isActive === (status === 'Active')).length}</strong></Link>)}</div>}
      </section>
    </div>
    {selected && <AdminDetails title="Trip details" onClose={() => setSelected(null)} fields={[['Trip ID',selected.id],['Pickup',selected.pickupLocation],['Drop-off',selected.dropoffLocation],['Capacity',selected.requiredCapacity],['Status',selected.status],['Driver',name(selected.assignedDriverId)],['Vehicle',vehicleName(selected.assignedVehicleId)],['Created',date(selected.createdAt)],['Updated',date(selected.updatedAt)]]}/>}
  </AppShell>
}
