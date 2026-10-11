import PageHeading from '../../components/common/PageHeading'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { MapContainer, TileLayer, CircleMarker, Tooltip, Popup } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import AppShell from '../../components/layout/AppShell'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import StatusBadge from '../../components/common/StatusBadge'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import EmptyState from '../../components/common/EmptyState'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useToast } from '../../context/ToastContext'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'
import * as trackingService from '../../services/trackingService'
import '../dispatcher/Tracking.css'

const FRESHNESS_COLOR = { fresh: '#2F8F5B', stale: '#D98E2B', unavailable: '#8A97A3' }
const TRACK_REFRESH_INTERVAL_MS = 15000 // same cadence as the Tracking page

function driverLabel(driver) {
  if (!driver) return null
  const name = [driver.firstName, driver.lastName].filter(Boolean).join(' ')
  return name || driver.username
}

function formatDateTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

const CANCELLABLE_STATUSES = ['Requested', 'Assigned', 'In-Progress']
const CANCEL_DISABLED_STATUSES = ['In-Progress']

export default function TripDetails() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [trip, setTrip] = useState(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [driversById, setDriversById] = useState({})

  const [suggestions, setSuggestions] = useState(null)
  const [suggestionsLoading, setSuggestionsLoading] = useState(false)
  const [suggestionsError, setSuggestionsError] = useState(null)

  const [driverList, setDriverList] = useState([])
  const [driversLoading, setDriversLoading] = useState(true)
  const [selectedDriverId, setSelectedDriverId] = useState('')

  const [confirmTarget, setConfirmTarget] = useState(null)
  const [assigning, setAssigning] = useState(false)
  const [assignError, setAssignError] = useState(null)
  const [assigned, setAssigned] = useState(false)

  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState(null)
  const [cancelled, setCancelled] = useState(false)

  const [trackOpen, setTrackOpen] = useState(false)
  const [trackLocation, setTrackLocation] = useState(null)
  const [trackLoading, setTrackLoading] = useState(false)
  const [trackError, setTrackError] = useState(null)
  const [trackLastRefreshedAt, setTrackLastRefreshedAt] = useState(null)
  const trackMountedRef = useRef(true)

  useEffect(() => {
    let cancelled = false
    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getTrip(id)
        if (!cancelled) setTrip(data)
      } catch (err) {
        if (!cancelled) {
          setLoadError(
            err.status === 404
              ? 'This trip could not be found. It may have been removed.'
              : err.message || 'Unable to load this trip.'
          )
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    load()
    return () => {
      cancelled = true
    }
  }, [id, reloadKey])

  useEffect(() => {
    let cancelled = false
    async function loadDrivers() {
      try {
        const data = await userService.listDrivers()
        if (cancelled) return
        setDriverList(data.filter(driver => driver.isActive))
        setDriversById(Object.fromEntries(data.map((d) => [d.id, d])))
      } catch {
        // Non-fatal: details still show, just without driver names/phones.
      } finally {
        if (!cancelled) setDriversLoading(false)
      }
    }
    loadDrivers()
    return () => {
      cancelled = true
    }
  }, [])

  // Only fetch ranked vehicles once we know the trip is actually awaiting
  // assignment - no point calling this for a Completed or In-Progress trip.
  useEffect(() => {
    if (!trip || trip.status !== 'Requested') return undefined
    let cancelled = false
    async function loadSuggestions() {
      setSuggestionsLoading(true)
      setSuggestionsError(null)
      try {
        const data = await tripService.getSuggestions(trip.id)
        if (!cancelled) setSuggestions(data)
      } catch (err) {
        if (!cancelled) setSuggestionsError(err.message || 'Unable to load vehicle suggestions.')
      } finally {
        if (!cancelled) setSuggestionsLoading(false)
      }
    }
    loadSuggestions()
    return () => {
      cancelled = true
    }
  }, [trip])

  // Inline "Track Trip" map: only polls while expanded and only while the
  // trip is still In-Progress (the tracking feed only has positions for
  // trips in that status - same rule the Tracking page follows).
  useEffect(() => {
    if (!trackOpen || !trip || trip.status !== 'In-Progress') return undefined
    trackMountedRef.current = true

    async function fetchLocation(isInitial) {
      if (isInitial) setTrackLoading(true)
      setTrackError(null)
      try {
        const fleet = await trackingService.getFleetLocations()
        if (!trackMountedRef.current) return
        const mine = fleet.find((loc) => loc.tripId === trip.id) || null
        setTrackLocation(mine)
        setTrackLastRefreshedAt(new Date())
      } catch (err) {
        if (!trackMountedRef.current) return
        setTrackError(err.message || 'Unable to load this trip’s live location.')
      } finally {
        if (isInitial && trackMountedRef.current) setTrackLoading(false)
      }
    }

    fetchLocation(true)
    const intervalId = window.setInterval(() => fetchLocation(false), TRACK_REFRESH_INTERVAL_MS)

    return () => {
      trackMountedRef.current = false
      window.clearInterval(intervalId)
    }
  }, [trackOpen, trip])

  async function handleConfirmAssign() {
    if (!confirmTarget || !selectedDriverId || !trip) return
    setAssigning(true)
    setAssignError(null)
    try {
      await tripService.assignVehicle(trip.id, confirmTarget.vehicleId, selectedDriverId)
      setAssigned(true)
      setConfirmTarget(null)
      const driver = driverList.find((d) => d.id === selectedDriverId)
      showToast(`${confirmTarget.registrationNo} assigned${driver ? ` to ${driverLabel(driver)}` : ''}.`, 'success')
      window.setTimeout(() => navigate('/dispatcher/trips'), 1500)
    } catch (err) {
      setConfirmTarget(null)
      setAssignError(
        err.status === 409
          ? (err.message || 'This vehicle is no longer available. It may have just been assigned to another trip.')
          : err.status === 404
            ? 'This trip or vehicle could not be found. It may have changed.'
            : err.message || 'Unable to assign this vehicle. Please try again.'
      )
    } finally {
      setAssigning(false)
    }
  }

  const selectedDriverForConfirm = driverList.find((d) => d.id === selectedDriverId) || null
  const needsAssignment = trip?.status === 'Requested'
  const wasRejected = needsAssignment && trip?.rejectedByDriverId

  async function handleConfirmCancel() {
    if (!trip) return
    setCancelling(true)
    setCancelError(null)
    try {
      const updated = await tripService.cancelTrip(trip.id)
      setTrip(updated)
      setCancelled(true)
      setCancelOpen(false)
      showToast('Trip cancelled.', 'success')
      window.setTimeout(() => navigate('/dispatcher/trips'), 1200)
    } catch (err) {
      setCancelOpen(false)
      setCancelError(
        err.status === 409
          ? (err.message || 'This trip can no longer be cancelled - it may have changed status.')
          : err.message || 'Unable to cancel this trip. Please try again.'
      )
    } finally {
      setCancelling(false)
    }
  }

  return (
    <AppShell title="Trip Details">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Dispatch operations" title="Trip Details" />
          <p className="page-header-desc">Full information for this trip, and vehicle assignment if it still needs one.</p>
        </div>
        <div className="page-header-actions">
          {trip && CANCELLABLE_STATUSES.includes(trip.status) && (
            <Button
              variant="secondary"
              className="btn-glass-danger"
              disabled={CANCEL_DISABLED_STATUSES.includes(trip.status) || cancelled}
              title={CANCEL_DISABLED_STATUSES.includes(trip.status) ? 'Cancel is disabled once the driver has accepted the trip' : undefined}
              onClick={() => { setCancelError(null); setCancelOpen(true) }}
            >
              Cancel Trip
            </Button>
          )}
          <Link to="/dispatcher/trips">
            <Button variant="secondary">Back to Trips</Button>
          </Link>
        </div>
      </div>

      {cancelError && (
        <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
          <ErrorMessage>{cancelError}</ErrorMessage>
        </div>
      )}

      {cancelled && (
        <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
          <SuccessMessage>Trip cancelled. Redirecting to Trips…</SuccessMessage>
        </div>
      )}

      {loading && <LoadingSpinner center label="Loading trip…" />}

      {!loading && loadError && (
        <div className="card card-pad">
          <ErrorMessage>{loadError}</ErrorMessage>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
          </div>
        </div>
      )}

      {!loading && !loadError && trip && (
        <>
          <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 'var(--space-3)' }}>
              <span className="cell-id">{trip.id.slice(0, 8)}</span>
              <StatusBadge status={trip.status} />
            </div>

            <div className="stack" style={{ gap: 'var(--space-3)' }}>
              <div>
                <div className="page-header-desc">Pickup</div>
                <div style={{ fontWeight: 600 }}>{trip.pickupLocation}</div>
              </div>
              <div>
                <div className="page-header-desc">Drop-off</div>
                <div style={{ fontWeight: 600 }}>{trip.dropoffLocation}</div>
              </div>
              <div>
                <div className="page-header-desc">Required capacity</div>
                <div style={{ fontWeight: 600 }}>{trip.requiredCapacity}</div>
              </div>
              <div>
                <div className="page-header-desc">Created</div>
                <div>{formatDateTime(trip.createdAt)}</div>
              </div>

              {trip.assignedDriverId && (
                <div>
                  <div className="page-header-desc">Driver</div>
                  <div style={{ fontWeight: 600 }}>
                    {driverLabel(driversById[trip.assignedDriverId]) || 'Assigned driver'}
                    {trip.status === 'Requested' ? ' · awaiting response' : ''}
                  </div>
                  {trip.status === 'In-Progress' && driversById[trip.assignedDriverId]?.contactNumber && (
                    <div className="page-header-desc">{driversById[trip.assignedDriverId].contactNumber}</div>
                  )}
                </div>
              )}
            </div>

            {trip.status === 'In-Progress' && (
              <div style={{ marginTop: 'var(--space-4)' }}>
                <Button variant="secondary" className="btn-glass" onClick={() => setTrackOpen((open) => !open)}>
                  <Icon name="pin" size={15} /> {trackOpen ? 'Hide Map' : 'Track Trip'}
                </Button>
              </div>
            )}
          </div>

          {trackOpen && trip.status === 'In-Progress' && (
            <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-3)' }}>
                <h3 className="section-title" style={{ margin: 0 }}>Live location</h3>
                {trackLastRefreshedAt && (
                  <span className="tracking-refreshed-at">Updated {trackLastRefreshedAt.toLocaleTimeString()}</span>
                )}
              </div>

              {trackLoading && <LoadingSpinner center label="Loading live location…" />}

              {!trackLoading && trackError && <ErrorMessage>{trackError}</ErrorMessage>}

              {!trackLoading && !trackError && (!trackLocation || trackLocation.latitude == null || trackLocation.longitude == null) && (
                <EmptyState
                  icon="pin"
                  title="No location reported yet"
                  description="This trip's driver hasn't sent a GPS position yet. The map will update automatically once one comes in."
                />
              )}

              {!trackLoading && !trackError && trackLocation && trackLocation.latitude != null && trackLocation.longitude != null && (
                <div className="map-shell" style={{ height: 360 }}>
                  <MapContainer center={[trackLocation.latitude, trackLocation.longitude]} zoom={13} style={{ height: '100%', width: '100%' }}>
                    <TileLayer
                      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                    />
                    <CircleMarker
                      center={[trackLocation.latitude, trackLocation.longitude]}
                      radius={10}
                      pathOptions={{
                        color: '#fff',
                        weight: 2,
                        fillColor: FRESHNESS_COLOR[trackLocation.freshness] || FRESHNESS_COLOR.unavailable,
                        fillOpacity: 1,
                      }}
                    >
                      <Tooltip direction="top" offset={[0, -8]} permanent>
                        {driverLabel(driversById[trip.assignedDriverId]) || 'Driver'}
                      </Tooltip>
                      <Popup>
                        <div style={{ fontSize: 13, lineHeight: 1.5 }}>
                          <div><strong>{trip.pickupLocation} → {trip.dropoffLocation}</strong></div>
                          <div>Last update: {formatDateTime(trackLocation.observedAt)}</div>
                        </div>
                      </Popup>
                    </CircleMarker>
                  </MapContainer>
                </div>
              )}
            </div>
          )}

          {wasRejected && (
            <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
              <div className="cell-warning">
                <Icon name="alert" size={14} />
                <span>
                  {driverLabel(driversById[trip.rejectedByDriverId]) || 'The previously assigned driver'} is busy — pick another driver and vehicle below.
                </span>
              </div>
            </div>
          )}

          {needsAssignment && (
            <>
              <div className="card card-pad" style={{ marginBottom: 'var(--space-4)' }}>
                <label className="font-title-md text-body-sm text-on-surface" htmlFor="driver-select">
                  Assign to driver
                </label>
                <select
                  id="driver-select"
                  className="select-input"
                  style={{ marginTop: 'var(--space-2)' }}
                  value={selectedDriverId}
                  onChange={(e) => setSelectedDriverId(e.target.value)}
                  disabled={driversLoading || assigned}
                >
                  <option value="">{driversLoading ? 'Loading drivers…' : '— Select a driver —'}</option>
                  {driverList.map((d) => (
                    <option key={d.id} value={d.id}>{driverLabel(d)}</option>
                  ))}
                </select>
              </div>

              <div className="card card-pad">
                <div className="font-title-md text-body-sm text-on-surface" style={{ marginBottom: 'var(--space-2)' }}>
                  Suggested vehicles
                </div>

                <SuccessMessage>{assigned ? 'Vehicle assigned. Redirecting to Trips…' : null}</SuccessMessage>
                <ErrorMessage>{assignError}</ErrorMessage>

                {suggestionsLoading && <LoadingSpinner center label="Finding suggestions…" />}

                {!suggestionsLoading && suggestionsError && <ErrorMessage>{suggestionsError}</ErrorMessage>}

                {!suggestionsLoading && !suggestionsError && suggestions && suggestions.length === 0 && (
                  <EmptyState
                    icon="truck"
                    title="No matching vehicles right now"
                    description="No Available vehicle currently has enough capacity, in the right unit, for this trip. Check back once one is free."
                  />
                )}

                {!suggestionsLoading && !suggestionsError && suggestions && suggestions.length > 0 && (
                  <div className="stack" style={{ gap: 'var(--space-3)' }}>
                    {suggestions.map((s, index) => (
                      <div
                        key={s.vehicleId}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: 'var(--space-4)',
                          padding: 'var(--space-4)',
                          borderRadius: 'var(--radius-lg)',
                          border: index === 0 ? '2px solid rgb(var(--sf-secondary))' : '1px solid var(--color-border)',
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                          <Icon name="truck" size={20} />
                          <div>
                            <div style={{ fontWeight: 600 }}>
                              {s.registrationNo} {index === 0 && <span style={{ color: 'rgb(var(--sf-secondary))' }}>· Best fit</span>}
                            </div>
                            <div className="page-header-desc">{s.type} · {s.capacity} capacity</div>
                          </div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)' }}>
                          <div style={{ textAlign: 'right' }}>
                            <div className="page-header-desc">Spare capacity</div>
                            <div style={{ fontWeight: 600 }}>{s.excessCapacity} {s.unit}</div>
                          </div>
                          <Button
                            variant={index === 0 ? 'accent' : 'secondary'}
                            disabled={assigned || !selectedDriverId}
                            onClick={() => setConfirmTarget(s)}
                          >
                            Assign
                          </Button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}

                {!suggestionsLoading && !suggestionsError && suggestions?.length > 0 && !selectedDriverId && (
                  <p className="font-body-sm text-body-sm text-on-surface-variant" style={{ marginTop: 'var(--space-3)' }}>
                    Select a driver above before assigning a vehicle.
                  </p>
                )}
              </div>
            </>
          )}
        </>
      )}

      <ConfirmDialog
        open={confirmTarget !== null}
        title="Assign this vehicle?"
        body={
          confirmTarget && selectedDriverForConfirm
            ? `Assign ${confirmTarget.registrationNo} to this trip and notify ${driverLabel(selectedDriverForConfirm)}?`
            : ''
        }
        confirmLabel="Assign vehicle"
        cancelLabel="Cancel"
        variant="accent"
        loading={assigning}
        onConfirm={handleConfirmAssign}
        onCancel={() => setConfirmTarget(null)}
      />

      <ConfirmDialog
        open={cancelOpen}
        title="Cancel this trip?"
        body={
          trip
            ? `Cancel the trip from ${trip.pickupLocation} to ${trip.dropoffLocation}? ${
                trip.status === 'Assigned'
                  ? 'The assigned vehicle will be freed up for new assignments.'
                  : 'This cannot be undone.'
              }`
            : ''
        }
        confirmLabel="Cancel trip"
        cancelLabel="Keep trip"
        variant="danger"
        loading={cancelling}
        onConfirm={handleConfirmCancel}
        onCancel={() => { if (!cancelling) setCancelOpen(false) }}
      />
    </AppShell>
  )
}