import { useEffect, useRef, useState } from 'react'
import { MapContainer, TileLayer, CircleMarker, Tooltip, Popup } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import PageHeading from '../../components/common/PageHeading'
import AppShell from '../../components/layout/AppShell'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import EmptyState from '../../components/common/EmptyState'
import * as trackingService from '../../services/trackingService'
import * as userService from '../../services/userService'
import * as vehicleService from '../../services/vehicleService'
import './Tracking.css'

// Vite bundles the marker image differently to how Leaflet's CSS expects it
// by default — same fix already used in LocationPicker.jsx.
delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

const DEFAULT_CENTER = [7.8731, 80.7718] // Sri Lanka — same default as LocationPicker.jsx

const FRESHNESS_COLOR = { fresh: '#2F8F5B', stale: '#D98E2B', unavailable: '#8A97A3' }

// SCRUM-127 agreed rules: poll every 15s, 5-minute staleness threshold
// (threshold itself is enforced server-side — see TrackingService.GetFleetLatestAsync).
const REFRESH_INTERVAL_MS = 15000

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

function shortId(id) {
  return id ? id.slice(0, 8) : '—'
}

// Same join pattern Trips.jsx uses for the Driver column.
function driverName(driver) {
  if (!driver) return null
  const name = [driver.firstName, driver.lastName].filter(Boolean).join(' ')
  return name || driver.username
}

export default function Tracking() {
  const [locations, setLocations] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [driversById, setDriversById] = useState({})
  const [vehiclesById, setVehiclesById] = useState({})
  const mountedRef = useRef(true)

  useEffect(() => {
    let cancelled = false

    userService.listDrivers()
      .then((data) => { if (!cancelled) setDriversById(Object.fromEntries(data.map((d) => [d.id, d]))) })
      .catch(() => {})

    vehicleService.listVehicles()
      .then((data) => { if (!cancelled) setVehiclesById(Object.fromEntries(data.map((v) => [v.id, v]))) })
      .catch(() => {})

    return () => { cancelled = true }
  }, [])

  function vehicleLabel(vehicleId) {
    return vehiclesById[vehicleId]?.registrationNo || shortId(vehicleId)
  }

  function driverLabel(driverId) {
    return driverName(driversById[driverId]) || shortId(driverId)
  }

  useEffect(() => {
    mountedRef.current = true

    async function fetchFleet(isInitial) {
      try {
        const data = await trackingService.getFleetLocations()
        if (!mountedRef.current) return
        setLocations(data)
        setLoadError(null)
        setLastRefreshedAt(new Date())
      } catch (err) {
        if (!mountedRef.current) return
        // AC 4: a tracking-service failure shows clear error feedback, but
        // previously retrieved markers stay on screen with their real
        // last-update times rather than being wiped.
        setLoadError(err.message || 'Unable to load live fleet locations.')
      } finally {
        if (isInitial && mountedRef.current) setLoading(false)
      }
    }

    fetchFleet(true)
    const intervalId = window.setInterval(() => fetchFleet(false), REFRESH_INTERVAL_MS)

    return () => {
      mountedRef.current = false
      window.clearInterval(intervalId)
    }
  }, [reloadKey])

  const plottable = locations.filter((l) => l.latitude != null && l.longitude != null)
  const center = plottable.length > 0
    ? [
        plottable.reduce((sum, l) => sum + l.latitude, 0) / plottable.length,
        plottable.reduce((sum, l) => sum + l.longitude, 0) / plottable.length,
      ]
    : DEFAULT_CENTER

  const showFullError = loadError && locations.length === 0 && !loading

  return (
    <AppShell title="Fleet Tracking">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Live tracking" title="Fleet Tracking" />
          <p className="page-header-desc">
            Live locations for every In-Progress trip, refreshed every 15 seconds.
          </p>
        </div>
        <div className="page-header-actions">
          {lastRefreshedAt && (
            <span className="tracking-refreshed-at">Updated {lastRefreshedAt.toLocaleTimeString()}</span>
          )}
          <Button variant="secondary" size="sm" onClick={() => setReloadKey((k) => k + 1)}>
            <Icon name="pin" size={14} /> Refresh now
          </Button>
        </div>
      </div>

      {loadError && !showFullError && (
        <div style={{ marginBottom: 'var(--space-4)' }}>
          <ErrorMessage>{loadError} Showing the last known positions below.</ErrorMessage>
        </div>
      )}

      {loading ? (
        <div className="card card-pad">
          <LoadingSpinner label="Loading live fleet locations…" center />
        </div>
      ) : showFullError ? (
        <div className="card card-pad">
          <ErrorMessage>{loadError}</ErrorMessage>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
          </div>
        </div>
      ) : locations.length === 0 ? (
        <div className="card card-pad">
          <EmptyState
            icon="pin"
            title="No trips in progress"
            description="The fleet map shows vehicles while their trip is In-Progress. Check back once a trip is underway."
          />
        </div>
      ) : (
        <div className="tracking-grid">
          <div className="map-shell">
            <MapContainer center={center} zoom={plottable.length > 0 ? 8 : 7} style={{ height: '100%', width: '100%' }}>
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              {plottable.map((loc) => (
                <CircleMarker
                  key={loc.tripId}
                  center={[loc.latitude, loc.longitude]}
                  radius={9}
                  pathOptions={{
                    color: '#fff',
                    weight: 2,
                    fillColor: FRESHNESS_COLOR[loc.freshness] || FRESHNESS_COLOR.unavailable,
                    fillOpacity: 1,
                  }}
                >
                  <Tooltip direction="top" offset={[0, -8]} permanent>
                    {loc.vehicleId ? vehicleLabel(loc.vehicleId) : 'Vehicle'}
                  </Tooltip>
                  <Popup>
                    <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                      <div><strong>Trip {shortId(loc.tripId)}</strong></div>
                      {loc.vehicleId && <div>Vehicle: {vehicleLabel(loc.vehicleId)}</div>}
                      {loc.driverId && <div>Driver: {driverLabel(loc.driverId)}</div>}
                      <div>{loc.pickupLocation} → {loc.dropoffLocation}</div>
                      <div>Last update: {formatDateTime(loc.observedAt)}</div>
                    </div>
                  </Popup>
                </CircleMarker>
              ))}
            </MapContainer>
          </div>

          <div className="vehicle-list-panel">
            {locations.map((loc) => (
              <div className="vehicle-list-item" key={loc.tripId}>
                <div>
                  <div style={{ fontWeight: 600, color: 'var(--ink-900)' }}>
                    {loc.vehicleId ? vehicleLabel(loc.vehicleId) : `Trip ${shortId(loc.tripId)}`}
                  </div>
                  <div style={{ fontSize: 11.5, color: 'var(--ink-500)' }}>
                    {loc.pickupLocation} → {loc.dropoffLocation}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--ink-300)' }}>
                    {loc.driverId ? `Driver: ${driverLabel(loc.driverId)} · ` : ''}
                    {loc.freshness === 'unavailable'
                      ? 'No location reported yet'
                      : `Updated ${formatDateTime(loc.observedAt)}`}
                  </div>
                </div>
                <StatusBadge status={loc.freshness} />
              </div>
            ))}
          </div>
        </div>
      )}
    </AppShell>
  )
}