import PageHeading from '../../components/common/PageHeading'
import { useEffect, useMemo, useRef, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import StatusBadge from '../../components/common/StatusBadge'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import EmptyState from '../../components/common/EmptyState'
import Button from '../../components/common/Button'
import DataTable from '../../components/common/DataTable'
import AdminDetails from '../../components/common/AdminDetails'
import { getFleetUtilizationReport, getTripCompletionReport } from '../../services/reportService'
import { getDriverPerformanceReport, listTrips } from '../../services/tripService'
import { listDrivers, getUser } from '../../services/userService'
import { FLEET_OVERVIEW } from '../../utils/mockData'
import './Reports.css'
// AdminDetails.jsx's own markup (.admin-detail-dialog etc.) and the plain
// "view trip" link style (.admin-text-button) are only defined in
// AdminDashboard.css - pulled in here too so the Trip Details modal below
// is styled correctly even when a Dispatcher opens this same page without
// ever having visited /admin/dashboard.
import '../admin/AdminDashboard.css'

const RANGE_DAYS = { '7d': 7, '30d': 30, '90d': 90 }
const RANGE_LABEL = { '7d': 'Last 7 days', '30d': 'Last 30 days', '90d': 'Last 90 days' }
const TRIP_STATUSES = ['All', 'Requested', 'Assigned', 'In-Progress', 'Completed', 'Cancelled']

function formatPeriod(start, end) {
  const fmt = (d) => d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  return `${fmt(start)} – ${fmt(end)}`
}

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

function personName(person) {
  if (!person) return null
  const name = [person.firstName, person.lastName].filter(Boolean).join(' ')
  return name || person.username
}

export default function Reports() {
  const [range, setRange] = useState('7d')

  const [utilization, setUtilization] = useState(null)
  const [utilLoading, setUtilLoading] = useState(true)
  const [utilError, setUtilError] = useState('')
  const [utilReloadKey, setUtilReloadKey] = useState(0)

  const [completion, setCompletion] = useState(null)
  const [completionLoading, setCompletionLoading] = useState(true)
  const [completionError, setCompletionError] = useState('')
  const [completionReloadKey, setCompletionReloadKey] = useState(0)

  const [driverReport, setDriverReport] = useState(null)
  const [driverLoading, setDriverLoading] = useState(true)
  const [driverError, setDriverError] = useState('')
  const [driverReloadKey, setDriverReloadKey] = useState(0)
  const [selectedDriverId, setSelectedDriverId] = useState('')


  const [allTrips, setAllTrips] = useState([])
  const [driversById, setDriversById] = useState({})
  const [dispatchersById, setDispatchersById] = useState({})
  const [tripsLoading, setTripsLoading] = useState(true)
  const [tripsError, setTripsError] = useState('')
  const [tripsReloadKey, setTripsReloadKey] = useState(0)
  const [tripStatusFilter, setTripStatusFilter] = useState('All')
  const [selectedTrip, setSelectedTrip] = useState(null)

  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true

    async function run() {
      try {
        const end = new Date()
        const start = new Date(end.getTime() - RANGE_DAYS[range] * 24 * 60 * 60 * 1000)
        const report = await getFleetUtilizationReport({ start, end })
        if (!mountedRef.current) return
        setUtilization(report)
        setUtilError('')
      } catch (err) {
        if (!mountedRef.current) return
        setUtilError(err?.message || 'Could not load the fleet utilization report.')
      } finally {
        if (mountedRef.current) setUtilLoading(false)
      }
    }

    run()

    return () => {
      mountedRef.current = false
    }
  }, [range, utilReloadKey])

  useEffect(() => {
    mountedRef.current = true

    async function run() {
      try {
        const end = new Date()
        const start = new Date(end.getTime() - RANGE_DAYS[range] * 24 * 60 * 60 * 1000)
        const report = await getTripCompletionReport({ start, end })
        if (!mountedRef.current) return
        setCompletion(report)
        setCompletionError('')
      } catch (err) {
        if (!mountedRef.current) return
        setCompletionError(err?.message || 'Could not load the trip completion report.')
      } finally {
        if (mountedRef.current) setCompletionLoading(false)
      }
    }

    run()

    return () => {
      mountedRef.current = false
    }
  }, [range, completionReloadKey])

  // SCRUM-149 (US-19): same shape as the two effects above. The Driver
  // dropdown below filters the already-fetched rows client-side, since the
  // API returns every driver's metrics for the period in one call.
  useEffect(() => {
    mountedRef.current = true

    async function run() {
      try {
        const end = new Date()
        const start = new Date(end.getTime() - RANGE_DAYS[range] * 24 * 60 * 60 * 1000)
        const report = await getDriverPerformanceReport({ start, end })
        if (!mountedRef.current) return
        setDriverReport(report)
        setSelectedDriverId('')
        setDriverError('')
      } catch (err) {
        if (!mountedRef.current) return
        setDriverError(err?.message || 'Could not load the driver performance report.')
      } finally {
        if (mountedRef.current) setDriverLoading(false)
      }
    }

    run()

    return () => {
      mountedRef.current = false
    }
  }, [range, driverReloadKey])

  useEffect(() => {
    let cancelled = false

    async function run() {
      setTripsLoading(true)
      try {
        const [trips, drivers] = await Promise.all([listTrips(), listDrivers()])
        if (cancelled) return
        setAllTrips(trips)
        setDriversById(Object.fromEntries(drivers.map((d) => [d.id, d])))
        setTripsError('')

        // Resolve each distinct Dispatcher referenced by these trips, once -
        // a lookup-by-id endpoint, not a bulk list, so dedupe first.
        const dispatcherIds = [...new Set(trips.map((t) => t.createdByDispatcherId).filter(Boolean))]
        const results = await Promise.all(
          dispatcherIds.map((id) => getUser(id).catch(() => null))
        )
        if (!cancelled) {
          setDispatchersById(Object.fromEntries(
            dispatcherIds.map((id, i) => [id, results[i]]).filter(([, u]) => u)
          ))
        }
      } catch (err) {
        if (!cancelled) setTripsError(err?.message || 'Could not load trip details.')
      } finally {
        if (!cancelled) setTripsLoading(false)
      }
    }

    run()

    return () => {
      cancelled = true
    }
  }, [tripsReloadKey])

  const visibleTrips = useMemo(() => {
    const end = new Date()
    const start = new Date(end.getTime() - RANGE_DAYS[range] * 24 * 60 * 60 * 1000)
    return allTrips
      .filter((t) => {
        const created = new Date(t.createdAt)
        if (Number.isNaN(created.getTime()) || created < start || created > end) return false
        return tripStatusFilter === 'All' || t.status === tripStatusFilter
      })
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
  }, [allTrips, range, tripStatusFilter])

  const tripColumns = [
    {
      key: 'id',
      header: 'Trip',
      render: (r) => (
        <button className="admin-text-button" onClick={() => setSelectedTrip(r)} aria-label={`View trip ${r.id}`}>
          {r.id.slice(0, 8)}
        </button>
      ),
    },
    {
      key: 'route',
      header: 'Route',
      render: (r) => <div><strong>{r.pickupLocation}</strong><span className="utilization-vehicle-type"> → {r.dropoffLocation}</span></div>,
    },
    { key: 'driver', header: 'Driver', render: (r) => personName(driversById[r.assignedDriverId]) || 'Not assigned' },
    { key: 'dispatcher', header: 'Dispatcher', render: (r) => personName(dispatchersById[r.createdByDispatcherId]) || '—' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdAt', header: 'Created', render: (r) => formatDateTime(r.createdAt) },
  ]

  const visibleDrivers = driverReport?.drivers.filter(
    (d) => !selectedDriverId || d.driverId === selectedDriverId
  ) ?? []

  return (
    <AppShell title="Reports">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Reporting & analytics" title="Reports" />
          <p className="page-header-desc">Fleet utilization, trip completion, and activity over time.</p>
        </div>
        <div className="page-header-actions">
          <select className="select-input" value={range} onChange={(e) => setRange(e.target.value)}>
            <option value="7d">Last 7 days</option>
            <option value="30d">Last 30 days</option>
            <option value="90d">Last 90 days</option>
          </select>
        </div>
      </div>

      <div className="report-grid">
        <div className="card card-pad">
          <div className="report-card-header">
            <h3 className="section-title">Fleet Utilization Report</h3>
            {!utilLoading && !utilError && (
              <Button variant="ghost" size="sm" onClick={() => setUtilReloadKey((k) => k + 1)}>Refresh</Button>
            )}
          </div>

          {utilLoading && <LoadingSpinner label="Calculating utilization…" center />}

          {!utilLoading && utilError && (
            <>
              <ErrorMessage>{utilError}</ErrorMessage>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <Button variant="secondary" size="sm" onClick={() => setUtilReloadKey((k) => k + 1)}>Try again</Button>
              </div>
            </>
          )}

          {!utilLoading && !utilError && utilization && utilization.vehicles.length === 0 && (
            <EmptyState
              icon="grid"
              title="No eligible vehicles"
              description="No vehicles were registered within the selected period."
            />
          )}

          {!utilLoading && !utilError && utilization && utilization.vehicles.length > 0 && (
            <>
              <div style={{ marginBottom: 'var(--space-3)' }}>
                <div className="report-metric-row"><span>Vehicles in report</span><span>{utilization.summary.totalVehicles}</span></div>
                <div className="report-metric-row"><span>Fleet-wide utilization</span><span>{utilization.summary.overallUtilizationPercent}%</span></div>
                <div className="report-metric-row"><span>Total in-use hours</span><span>{utilization.summary.totalInUseHours}h</span></div>
                {utilization.summary.vehiclesWithInsufficientData > 0 && (
                  <div className="report-metric-row">
                    <span>Vehicles with limited data</span>
                    <span>{utilization.summary.vehiclesWithInsufficientData}</span>
                  </div>
                )}
              </div>

              <table className="utilization-table">
                <thead>
                  <tr>
                    <th>Vehicle</th>
                    <th>Trips</th>
                    <th>In-use hours</th>
                    <th>Utilization</th>
                  </tr>
                </thead>
                <tbody>
                  {utilization.vehicles.map((v) => (
                    <tr key={v.vehicleId}>
                      <td>{v.registrationNo} <span className="utilization-vehicle-type">· {v.vehicleType}</span></td>
                      <td>{v.completedTripCount}</td>
                      <td>{v.inUseHours}h</td>
                      <td>
                        {v.utilizationPercent}%
                        {v.hasInsufficientData && <StatusBadge status="Limited data" />}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>

        <div className="card card-pad">
          <div className="report-card-header">
            <h3 className="section-title">Trip Completion Report</h3>
            {!completionLoading && !completionError && (
              <Button variant="ghost" size="sm" onClick={() => setCompletionReloadKey((k) => k + 1)}>Refresh</Button>
            )}
          </div>

          {completionLoading && <LoadingSpinner label="Calculating trip completion…" center />}

          {!completionLoading && completionError && (
            <>
              <ErrorMessage>{completionError}</ErrorMessage>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <Button variant="secondary" size="sm" onClick={() => setCompletionReloadKey((k) => k + 1)}>Try again</Button>
              </div>
            </>
          )}

          {!completionLoading && !completionError && completion && !completion.hasData && (
            <EmptyState
              icon="grid"
              title="No trips in this period"
              description="No trips were created within the selected period."
            />
          )}

          {!completionLoading && !completionError && completion && completion.hasData && (
            <div>
              <div className="report-metric-row"><span>Eligible trips</span><span>{completion.eligibleTripCount}</span></div>
              <div className="report-metric-row"><span>Completed</span><span>{completion.completedCount}</span></div>
              <div className="report-metric-row"><span>In progress</span><span>{completion.inProgressCount}</span></div>
              <div className="report-metric-row"><span>Assigned</span><span>{completion.assignedCount}</span></div>
              <div className="report-metric-row"><span>Requested</span><span>{completion.requestedCount}</span></div>
              <div className="report-metric-row"><span>Cancelled</span><span>{completion.cancelledCount}</span></div>
              <div className="report-metric-row"><span>Completion rate</span><span>{completion.completionRatePercent}%</span></div>
            </div>
          )}
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Vehicle Status Overview</h3>
          <p className="report-demo-note">Demo data.</p>
          <div>
            <div className="report-metric-row"><StatusBadge status="Available" /><span>{FLEET_OVERVIEW.available}</span></div>
            <div className="report-metric-row"><StatusBadge status="In Use" /><span>{FLEET_OVERVIEW.inUse}</span></div>
            <div className="report-metric-row"><StatusBadge status="Maintenance" /><span>{FLEET_OVERVIEW.maintenance}</span></div>
            <div className="report-metric-row"><StatusBadge status="Inactive" /><span>{FLEET_OVERVIEW.inactive}</span></div>
          </div>
        </div>

        <div className="card card-pad">
          <div className="report-card-header">
            <h3 className="section-title">Driver Performance Report</h3>
            {!driverLoading && !driverError && (
              <Button variant="ghost" size="sm" onClick={() => setDriverReloadKey((k) => k + 1)}>Refresh</Button>
            )}
          </div>

          {driverLoading && <LoadingSpinner label="Calculating driver performance…" center />}

          {!driverLoading && driverError && (
            <>
              <ErrorMessage>{driverError}</ErrorMessage>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <Button variant="secondary" size="sm" onClick={() => setDriverReloadKey((k) => k + 1)}>Try again</Button>
              </div>
            </>
          )}

          {!driverLoading && !driverError && driverReport && (
            <>
              <p className="report-demo-note">
                {formatPeriod(new Date(driverReport.periodStart), new Date(driverReport.periodEnd))} ({RANGE_LABEL[range]}).{' '}
                Accepted and Completed are counted from a driver's own assignments; Rejected reflects the most recent
                rejection recorded against a trip, even after it was reassigned. No combined score or ranking is shown.
              </p>

              {driverReport.drivers.length > 0 && (
                <div style={{ marginBottom: 'var(--space-3)' }}>
                  <select
                    className="select-input"
                    value={selectedDriverId}
                    onChange={(e) => setSelectedDriverId(e.target.value)}
                  >
                    <option value="">All drivers</option>
                    {driverReport.drivers.map((d) => (
                      <option key={d.driverId} value={d.driverId}>{d.firstName} {d.lastName}</option>
                    ))}
                  </select>
                </div>
              )}

              {driverReport.drivers.length === 0 && (
                <EmptyState
                  icon="user"
                  title="No driver accounts"
                  description="There are no Driver accounts to report on."
                />
              )}

              {driverReport.drivers.length > 0 && visibleDrivers.length === 0 && (
                <EmptyState
                  icon="user"
                  title="No matching driver"
                  description="The selected driver could not be found in this report."
                />
              )}

              {visibleDrivers.length > 0 && (
                <table className="utilization-table">
                  <thead>
                    <tr>
                      <th>Driver</th>
                      <th>Accepted</th>
                      <th>Completed</th>
                      <th>Rejected</th>
                      <th>Activity</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleDrivers.map((d) => (
                      <tr key={d.driverId}>
                        <td>
                          {d.firstName} {d.lastName}{' '}
                          <StatusBadge status={d.isActive ? 'Active' : 'Inactive'} />
                        </td>
                        <td>{d.acceptedCount}</td>
                        <td>{d.completedCount}</td>
                        <td>{d.rejectedCount}</td>
                        <td>{!d.hasActivity && <StatusBadge status="No activity" />}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>

        <div className="card card-pad report-grid-full">
          <div className="report-card-header">
            <h3 className="section-title">Trip Details</h3>
            {!tripsLoading && !tripsError && (
              <Button variant="ghost" size="sm" onClick={() => setTripsReloadKey((k) => k + 1)}>Refresh</Button>
            )}
          </div>
          <p className="report-demo-note">{RANGE_LABEL[range]}. Every trip created in the selected period, regardless of status.</p>

          {tripsLoading && <LoadingSpinner label="Loading trips…" center />}

          {!tripsLoading && tripsError && (
            <>
              <ErrorMessage>{tripsError}</ErrorMessage>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <Button variant="secondary" size="sm" onClick={() => setTripsReloadKey((k) => k + 1)}>Try again</Button>
              </div>
            </>
          )}

          {!tripsLoading && !tripsError && (
            <>
              <div style={{ marginBottom: 'var(--space-3)' }}>
                <select
                  className="select-input"
                  value={tripStatusFilter}
                  onChange={(e) => setTripStatusFilter(e.target.value)}
                >
                  {TRIP_STATUSES.map((s) => (
                    <option key={s} value={s}>{s === 'All' ? 'All statuses' : s}</option>
                  ))}
                </select>
              </div>

              <DataTable
                columns={tripColumns}
                rows={visibleTrips}
                loading={false}
                rowKey="id"
                emptyTitle="No trips in this period"
                emptyDescription="Try a different date range or status."
              />
            </>
          )}
        </div>
      </div>

      {selectedTrip && (
        <AdminDetails
          title="Trip details"
          onClose={() => setSelectedTrip(null)}
          fields={[
            ['Trip ID', selectedTrip.id],
            ['Pickup', selectedTrip.pickupLocation],
            ['Drop-off', selectedTrip.dropoffLocation],
            ['Capacity', selectedTrip.requiredCapacity],
            ['Status', selectedTrip.status],
            ['Driver', personName(driversById[selectedTrip.assignedDriverId]) || 'Not assigned'],
            ['Dispatcher', personName(dispatchersById[selectedTrip.createdByDispatcherId]) || 'Unknown'],
            ['Created', formatDateTime(selectedTrip.createdAt)],
            ['Updated', formatDateTime(selectedTrip.updatedAt)],
          ]}
        />
      )}
    </AppShell>
  )
}