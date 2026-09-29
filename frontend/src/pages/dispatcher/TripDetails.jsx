import PageHeading from '../../components/common/PageHeading'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
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

  return (
    <AppShell title="Trip Details">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Dispatch operations" title="Trip Details" />
          <p className="page-header-desc">Full information for this trip, and vehicle assignment if it still needs one.</p>
        </div>
        <div className="page-header-actions">
          <Link to="/dispatcher/trips">
            <Button variant="secondary">Back to Trips</Button>
          </Link>
        </div>
      </div>

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
          </div>

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
    </AppShell>
  )
}