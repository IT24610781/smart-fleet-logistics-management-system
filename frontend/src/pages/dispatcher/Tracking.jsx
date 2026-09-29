import PageHeading from '../../components/common/PageHeading'
import AppShell from '../../components/layout/AppShell'
import StatusBadge from '../../components/common/StatusBadge'
import { MOCK_VEHICLES } from '../../utils/mockData'
import './Tracking.css'

const PIN_COLOR = { Available: 'var(--success)', 'In Use': 'var(--info)', Maintenance: 'var(--warning)', Inactive: 'var(--offline)' }
// Fixed demo coordinates within the placeholder canvas — trackingService.js
// already exposes getFleetLocations() for when live telemetry replaces this.
const POSITIONS = [
  { x: '28%', y: '32%' }, { x: '58%', y: '48%' }, { x: '40%', y: '68%' },
  { x: '72%', y: '28%' }, { x: '18%', y: '58%' }, { x: '82%', y: '62%' },
]

export default function Tracking() {
  return (
    <AppShell title="Fleet Tracking">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Live tracking" title="Fleet Tracking" />
          <p className="page-header-desc">Live vehicle locations. Connects to the Tracking/Telemetry Service once available — positions and timestamps below are demo data.</p>
        </div>
      </div>

      <div className="tracking-grid">
        <div className="map-placeholder">
          {MOCK_VEHICLES.map((v, i) => (
            <div className="map-pin" key={v.id} style={{ left: POSITIONS[i % POSITIONS.length].x, top: POSITIONS[i % POSITIONS.length].y }}>
              <span className="map-pin-label">{v.id}</span>
              <span className="map-pin-dot" style={{ background: PIN_COLOR[v.status] || 'var(--offline)' }} />
            </div>
          ))}
          <span className="map-note">Map placeholder — live map service not yet integrated</span>
        </div>

        <div className="vehicle-list-panel">
          {MOCK_VEHICLES.map((v) => (
            <div className="vehicle-list-item" key={v.id}>
              <div>
                <div style={{ fontWeight: 600, color: 'var(--ink-900)' }}>{v.id}</div>
                <div style={{ fontSize: 11.5, color: 'var(--ink-500)' }}>{v.location}</div>
                <div style={{ fontSize: 11, color: 'var(--ink-300)' }}>
                  {v.currentDriver !== '—' ? `Driver: ${v.currentDriver} · ` : ''}Updated {v.lastUpdate}
                </div>
              </div>
              <StatusBadge status={v.status} />
            </div>
          ))}
        </div>
      </div>
    </AppShell>
  )
}
