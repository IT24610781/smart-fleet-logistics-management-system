import { useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import MiniChart from '../../components/common/MiniChart'
import StatusBadge from '../../components/common/StatusBadge'
import { MOCK_FLEET_UTILIZATION, MOCK_TRIP_COMPLETION, FLEET_OVERVIEW, DRIVER_OVERVIEW, TOP_DRIVERS_BY_TRIPS } from '../../utils/mockData'
import './Reports.css'

// Shared by Admin and Dispatcher (both are authorized per the SRS) — one
// implementation instead of duplicating the page under each role folder.
// reportService.js is already wired for getFleetUtilizationReport() /
// getTripCompletionReport() for when the Reporting service is live.
// Maintenance overview is intentionally omitted — there's no maintenance
// data model yet, and a chart with invented numbers wouldn't be meaningful.
export default function Reports() {
  const [range, setRange] = useState('7d')
  const fu = MOCK_FLEET_UTILIZATION
  const tc = MOCK_TRIP_COMPLETION

  return (
    <AppShell title="Reports">
      <div className="page-header">
        <div>
          <div className="page-header-title">Reports</div>
          <p className="page-header-desc">Fleet utilization, trip completion, and activity over time. Demo data until the Reporting service is live.</p>
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
          <h3 className="section-title">Fleet Utilization Report</h3>
          <MiniChart values={fu.trend} color="var(--brand-500)" />
          <div style={{ marginTop: 'var(--space-3)' }}>
            <div className="report-metric-row"><span>Total vehicles</span><span>{fu.totalVehicles}</span></div>
            <div className="report-metric-row"><span>Available vehicles</span><span>{fu.availableVehicles}</span></div>
            <div className="report-metric-row"><span>Active vehicles</span><span>{fu.activeVehicles}</span></div>
            <div className="report-metric-row"><span>Utilization</span><span>{fu.utilizationPercent}%</span></div>
          </div>
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Trip Completion Report</h3>
          <MiniChart values={tc.trend} color="var(--beacon)" />
          <div style={{ marginTop: 'var(--space-3)' }}>
            <div className="report-metric-row"><span>Total trips</span><span>{tc.totalTrips}</span></div>
            <div className="report-metric-row"><span>Completed</span><span>{tc.completed}</span></div>
            <div className="report-metric-row"><span>Pending</span><span>{tc.pending}</span></div>
            <div className="report-metric-row"><span>Rejected</span><span>{tc.rejected}</span></div>
            <div className="report-metric-row"><span>Completion rate</span><span>{tc.completionPercent}%</span></div>
          </div>
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Vehicle Status Overview</h3>
          <div>
            <div className="report-metric-row"><StatusBadge status="Available" /><span>{FLEET_OVERVIEW.available}</span></div>
            <div className="report-metric-row"><StatusBadge status="In Use" /><span>{FLEET_OVERVIEW.inUse}</span></div>
            <div className="report-metric-row"><StatusBadge status="Maintenance" /><span>{FLEET_OVERVIEW.maintenance}</span></div>
            <div className="report-metric-row"><StatusBadge status="Inactive" /><span>{FLEET_OVERVIEW.inactive}</span></div>
          </div>
        </div>

        <div className="card card-pad">
          <h3 className="section-title">Driver Activity</h3>
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
