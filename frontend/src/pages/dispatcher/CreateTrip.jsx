import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import LocationPicker from '../../components/forms/LocationPicker'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import EmptyState from '../../components/common/EmptyState'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useToast } from '../../context/ToastContext'
import * as tripService from '../../services/tripService'
import * as userService from '../../services/userService'
import { isRequired, isValidCapacity } from '../../utils/validation'

const EMPTY_LOCATION = { address: '', lat: null, lng: null }
const EMPTY_FORM = { pickup: EMPTY_LOCATION, dropoff: EMPTY_LOCATION, requiredCapacity: '' }
const CAPACITY_DEBOUNCE_MS = 600

function driverLabel(driver) {
  const name = [driver.firstName, driver.lastName].filter(Boolean).join(' ')
  return name || driver.username
}

// US-09/US-10/US-11 as one single action: fill in the trip

export default function CreateTrip() {
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [form, setForm] = useState(EMPTY_FORM)
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  const [suggestions, setSuggestions] = useState(null)
  const [suggestionsLoading, setSuggestionsLoading] = useState(false)
  const [suggestionsError, setSuggestionsError] = useState(null)

  const [drivers, setDrivers] = useState([])
  const [driversLoading, setDriversLoading] = useState(true)
  const [driversError, setDriversError] = useState(null)
  const [selectedDriverId, setSelectedDriverId] = useState('')
  const [selectedVehicle, setSelectedVehicle] = useState(null)

  const [confirmOpen, setConfirmOpen] = useState(false)
  const [created, setCreated] = useState(false)

  // Drivers rarely change mid-session - load them once, up front.
  useEffect(() => {
    let cancelled = false
    async function loadDrivers() {
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

  useEffect(() => {
    setSelectedVehicle(null)

    if (!isValidCapacity(form.requiredCapacity)) {
      setSuggestions(null)
      setSuggestionsError(null)
      return undefined
    }

    let cancelled = false
    const timer = window.setTimeout(async () => {
      setSuggestionsLoading(true)
      setSuggestionsError(null)
      try {
        const data = await tripService.previewSuggestions(form.requiredCapacity)
        if (!cancelled) setSuggestions(data)
      } catch (err) {
        if (!cancelled) setSuggestionsError(err.message || 'Unable to load vehicle suggestions.')
      } finally {
        if (!cancelled) setSuggestionsLoading(false)
      }
    }, CAPACITY_DEBOUNCE_MS)

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [form.requiredCapacity])

  function validate() {
    const errs = {}
    if (!isRequired(form.pickup.address)) errs.pickup = 'Pickup location is required.'
    if (!isRequired(form.dropoff.address)) errs.dropoff = 'Drop-off location is required.'
    if (!isRequired(form.requiredCapacity)) errs.requiredCapacity = 'Required capacity is required.'
    if (selectedVehicle && !selectedDriverId) errs.driver = 'Select a driver to go with the chosen vehicle.'
    return errs
  }

  function handleCreateClick(e) {
    e.preventDefault()
    setFormError(null)
    const errs = validate()
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    if (selectedVehicle) {
      setConfirmOpen(true)
    } else {
      doCreate()
    }
  }

  async function doCreate() {
    setConfirmOpen(false)
    setSubmitting(true)
    setFormError(null)
    try {
      const trip = await tripService.createTrip({
        pickupLocation: form.pickup.address,
        pickupLat: form.pickup.lat,
        pickupLng: form.pickup.lng,
        dropoffLocation: form.dropoff.address,
        dropoffLat: form.dropoff.lat,
        dropoffLng: form.dropoff.lng,
        requiredCapacity: form.requiredCapacity,
      })

      if (selectedVehicle && selectedDriverId) {
        try {
          await tripService.assignVehicle(trip.id, selectedVehicle.vehicleId, selectedDriverId)
          const driver = drivers.find((d) => d.id === selectedDriverId)
          showToast(`Trip created and assigned to ${driver ? driverLabel(driver) : 'the selected driver'}.`, 'success')
        } catch (err) {
          showToast(
            err.status === 409
              ? 'Trip created, but that vehicle was just taken by another trip. Assign one from Trips instead.'
              : 'Trip created, but the vehicle could not be assigned. Assign one from Trips instead.',
            'error'
          )
        }
      } else {
        showToast('Trip created.', 'success')
      }

      setCreated(true)
      window.setTimeout(() => navigate('/dispatcher/trips'), 1200)
    } catch (err) {
      if (err.fieldErrors) {
        setFieldErrors((f) => ({ ...f, ...err.fieldErrors }))
        setFormError('Please fix the highlighted fields.')
      } else {
        setFormError(err.message || 'Something went wrong. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const selectedDriver = drivers.find((d) => d.id === selectedDriverId) || null

  return (
    <AppShell title="Create Trip">
      <div className="page-header">
        <div>
          <div className="page-header-title">Create Trip</div>
          <p className="page-header-desc">
            Fill in the trip. As soon as the capacity looks valid, matching vehicles appear below - pick one and a
            driver if you want to assign it right away, then press Create Trip.
          </p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 640 }}>
        <form className="stack" onSubmit={handleCreateClick} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{created ? 'Trip created. Redirecting to Trips…' : null}</SuccessMessage>

          <LocationPicker
            label="Pickup location"
            value={form.pickup}
            onChange={(v) => setForm((f) => ({ ...f, pickup: v }))}
            error={fieldErrors.pickup}
            required
            placeholder="e.g. Kegalle Bus Stand"
          />
          <LocationPicker
            label="Drop-off location"
            value={form.dropoff}
            onChange={(v) => setForm((f) => ({ ...f, dropoff: v }))}
            error={fieldErrors.dropoff}
            required
            placeholder="e.g. Colombo Fort"
            showCurrentLocation={false}
          />
          <FormField
            label="Required capacity"
            name="requiredCapacity"
            value={form.requiredCapacity}
            onChange={(e) => setForm((f) => ({ ...f, requiredCapacity: e.target.value }))}
            error={fieldErrors.requiredCapacity}
            required
            hint="A number with an optional unit, e.g. 500 kg, 8 persons"
          />

          <div className="flex flex-col gap-1.5">
            <label className="font-title-md text-body-sm text-on-surface" htmlFor="driver-select">
              Assign to driver (optional)
            </label>
            <select
              id="driver-select"
              className="select-input"
              value={selectedDriverId}
              onChange={(e) => setSelectedDriverId(e.target.value)}
              disabled={driversLoading}
            >
              <option value="">{driversLoading ? 'Loading drivers…' : '— No driver selected yet —'}</option>
              {drivers.map((d) => (
                <option key={d.id} value={d.id}>{driverLabel(d)}</option>
              ))}
            </select>
            {driversError && <ErrorMessage>{driversError}</ErrorMessage>}
            {fieldErrors.driver && (
              <span className="font-body-sm text-body-sm text-error">{fieldErrors.driver}</span>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <label className="font-title-md text-body-sm text-on-surface">Matching vehicles</label>

            {!suggestionsLoading && !suggestionsError && suggestions === null && (
              <p className="font-body-sm text-body-sm text-on-surface-variant">
                Enter a required capacity above to see vehicles that fit.
              </p>
            )}

            {suggestionsLoading && <LoadingSpinner label="Finding vehicles…" />}

            {!suggestionsLoading && suggestionsError && <ErrorMessage>{suggestionsError}</ErrorMessage>}

            {!suggestionsLoading && !suggestionsError && suggestions && suggestions.length === 0 && (
              <EmptyState
                icon="truck"
                title="No matching vehicles right now"
                description="You can still create the trip - assign a vehicle later from Trips once one is free."
              />
            )}

            {!suggestionsLoading && !suggestionsError && suggestions && suggestions.length > 0 && (
              <div className="stack" style={{ gap: 'var(--space-2)' }}>
                {suggestions.map((s, index) => {
                  const isSelected = selectedVehicle?.vehicleId === s.vehicleId
                  return (
                    <button
                      type="button"
                      key={s.vehicleId}
                      onClick={() => setSelectedVehicle(isSelected ? null : s)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        gap: 'var(--space-3)',
                        padding: 'var(--space-3)',
                        borderRadius: 'var(--radius-lg)',
                        border: isSelected ? '2px solid var(--color-accent)' : '1px solid var(--color-border)',
                        background: isSelected ? 'var(--color-accent-container, #eef6f6)' : 'transparent',
                        cursor: 'pointer',
                        textAlign: 'left',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                        <Icon name={isSelected ? 'check' : 'truck'} size={18} />
                        <div>
                          <div style={{ fontWeight: 600 }}>
                            {s.registrationNo} {index === 0 && <span style={{ color: 'var(--color-accent)' }}>· Best fit</span>}
                          </div>
                          <div className="page-header-desc">{s.type} · {s.capacity} capacity</div>
                        </div>
                      </div>
                      <div className="page-header-desc">{s.excessCapacity} {s.unit} spare</div>
                    </button>
                  )
                })}
              </div>
            )}
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            <Button type="submit" loading={submitting} disabled={created}>
              {submitting ? 'Creating…' : 'Create trip'}
            </Button>
          </div>
        </form>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        title="Create and assign this trip?"
        body={
          selectedVehicle && selectedDriver
            ? `Create this trip and immediately assign ${selectedVehicle.registrationNo} with driver ${driverLabel(selectedDriver)}? The vehicle will be marked On Trip, and ${selectedDriver.firstName || 'the driver'} will be asked to accept or reject it.`
            : ''
        }
        confirmLabel="Create & assign"
        cancelLabel="Cancel"
        variant="accent"
        loading={submitting}
        onConfirm={doCreate}
        onCancel={() => setConfirmOpen(false)}
      />
    </AppShell>
  )
}