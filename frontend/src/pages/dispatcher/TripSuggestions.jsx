import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import EmptyState from '../../components/common/EmptyState'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useToast } from '../../context/ToastContext'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'

function driverLabel(driver) {
  const name = [driver.firstName, driver.lastName].filter(Boolean).join(' ')
  return name ? `${name} (@${driver.username})` : driver.username
}

// US-10/US-11/US-12: ranked vehicle suggestions for one trip. Assigning
// requires picking BOTH a vehicle and a driver - the driver then accepts or
// rejects this specific offer from their own My Trips page.
export default function TripSuggestions() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [suggestions, setSuggestions] = useState(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [drivers, setDrivers] = useState([])
  const [driversLoading, setDriversLoading] = useState(true)
  const [driversError, setDriversError] = useState(null)
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
        const data = await tripService.getSuggestions(id)
        if (cancelled) return
        setSuggestions(data)
      } catch (err) {
        if (!cancelled) {
          setLoadError(
            err.status === 404
              ? 'This trip could not be found. It may have been removed.'
              : err.message || 'Unable to load vehicle suggestions.'
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
      setDriversLoading(true)
      setDriversError(null)
      try {
        const data = await userService.listDrivers()
        if (!cancelled) setDrivers(data)
      } catch (err) {
        if (!cancelled) setDriversError(err.message || 'Unable to load drivers.')
      } finally {
        if (!cancelled) setDriversLoading(false)
      }
    }

    loadDrivers()
    return () => {
      cancelled = true
    }
  }, [])

  const selectedDriver = drivers.find((d) => d.id === selectedDriverId) || null

  async function handleConfirmAssign() {
    if (!confirmTarget || !selectedDriverId) return
    setAssigning(true)
    setAssignError(null)
    try {
      await tripService.assignVehicle(id, confirmTarget.vehicleId, selectedDriverId)
      setAssigned(true)
      setConfirmTarget(null)
      showToast(`${confirmTarget.registrationNo} assigned to this trip.`, 'success')
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

  return (
    <AppShell title="Vehicle Suggestions">
      <div className="page-header">
        <div>
          <div className="page-header-title">Suggested Vehicles</div>
          <p className="page-header-desc">
            Available vehicles that fit this trip's required capacity, ranked by the closest fit.
          </p>
        </div>
        <div className="page-header-actions">
          <Link to="/dispatcher/trips">
            <Button variant="secondary">Back to Trips</Button>
          </Link>
        </div>
      </div>

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
          <option value="">
            {driversLoading ? 'Loading drivers…' : '— Select a driver —'}
          </option>
          {drivers.map((d) => (
            <option key={d.id} value={d.id}>{driverLabel(d)}</option>
          ))}
        </select>
        {driversError && (
          <div style={{ marginTop: 'var(--space-2)' }}>
            <ErrorMessage>{driversError}</ErrorMessage>
          </div>
        )}
        {!driversLoading && !driversError && drivers.length === 0 && (
          <p className="font-body-sm text-body-sm text-on-surface-variant" style={{ marginTop: 'var(--space-2)' }}>
            No driver accounts exist yet. Ask an Admin to create one before assigning this trip.
          </p>
        )}
      </div>

      <div className="card card-pad">
        <SuccessMessage>{assigned ? 'Vehicle assigned. Redirecting to Trips…' : null}</SuccessMessage>
        <ErrorMessage>{assignError}</ErrorMessage>

        {loading && <LoadingSpinner center label="Finding suggestions…" />}

        {!loading && loadError && (
          <>
            <ErrorMessage>{loadError}</ErrorMessage>
            <div style={{ marginTop: 'var(--space-3)' }}>
              <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
            </div>
          </>
        )}

        {!loading && !loadError && suggestions && suggestions.length === 0 && (
          <EmptyState
            icon="truck"
            title="No matching vehicles right now"
            description="No Available vehicle currently has enough capacity, in the right unit, for this trip."
          />
        )}

        {!loading && !loadError && suggestions && suggestions.length > 0 && (
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
                  border: index === 0 ? '2px solid var(--color-accent)' : '1px solid var(--color-border)',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                  <Icon name="truck" size={20} />
                  <div>
                    <div style={{ fontWeight: 600 }}>
                      {s.registrationNo} {index === 0 && <span style={{ color: 'var(--color-accent)' }}>· Best fit</span>}
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

        {!loading && !loadError && suggestions && suggestions.length > 0 && !selectedDriverId && (
          <p className="font-body-sm text-body-sm text-on-surface-variant" style={{ marginTop: 'var(--space-3)' }}>
            Select a driver above before assigning a vehicle.
          </p>
        )}
      </div>

      <ConfirmDialog
        open={confirmTarget !== null}
        title="Assign this vehicle?"
        body={
          confirmTarget && selectedDriver
            ? `Assign ${confirmTarget.registrationNo} (${confirmTarget.type}) and driver ${driverLabel(selectedDriver)} to this trip? The vehicle will be marked On Trip, and ${selectedDriver.firstName || 'the driver'} will be asked to accept or reject it.`
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