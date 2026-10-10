import PageHeading from '../../components/common/PageHeading'
import { useEffect, useRef, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import StatusBadge from '../../components/common/StatusBadge'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import EmptyState from '../../components/common/EmptyState'
import Button from '../../components/common/Button'
import { getFleetUtilizationReport, getTripCompletionReport } from '../../services/reportService'
import { FLEET_OVERVIEW, DRIVER_OVERVIEW, TOP_DRIVERS_BY_TRIPS } from '../../utils/mockData'
import './Reports.css'

const RANGE_DAYS = { '7d': 7, '30d': 30, '90d': 90 }

// Shared by Admin and Dispatcher (both are authorized per the SRS) — one
// implementation instead of duplicating the page under each role folder.
// Vehicle Status / Driver Activity are still demo data — US-19 has no
// backend yet. Fleet Utilization (SCRUM-138/139, US-17) and Trip Completion
// (SCRUM-142/143, US-18) are both live.
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
          <h3 className="section-title">Driver Activity</h3>
          <p className="report-demo-note">Demo data until US-19 is implemented.</p>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <div className="report-metric-row"><span>Total drivers</span><span>{DRIVER_OVERVIEW.total}</span></div>
            <div className="report-metric-row"><StatusBadge status="Active" /><span>{DRIVER_OVERVIEW.active}</span></div>
            <div className="report-metric-row"><StatusBadge status="Inactive" /><span>{DRIVER_OVERVIEW.inactive}</span></div>
          </div>
          <h3 className="section-title" style={{ fontSize: 12.5, color: 'var(--ink-500)' }}>Top drivers by completed trips</h3>
          {TOP_DRIVERS_BY_TRIPS.map((d) => (
            <div className="report-metric-row" key={d.name}><span>{d.name}</span><span>{d.trips}</span></div>
          ))}
        </div>
      </div>
    </AppShell>
  )
}