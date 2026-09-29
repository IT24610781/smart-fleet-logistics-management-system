import PageHeading from '../../components/common/PageHeading'
import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import AppShell from '../../components/layout/AdminShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as vehicleService from '../../services/vehicleService'
import { isRequired, isValidCapacity, CAPACITY_HINT, CAPACITY_ERROR } from '../../utils/validation'

const STATUSES = ['Available', 'On Trip', 'Under Maintenance']

//US-06 Admin updates a vehicle's details and status.
export default function EditVehicle() {
  const { id } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const [vehicle, setVehicle] = useState(location.state?.vehicle || null)
  const [loadingVehicle,setLoadingVehicle] = useState(true)
  const [loadError,setLoadError] = useState('')
  const [retry,setRetry] = useState(0)

  // While a vehicle is On Trip, a Dispatcher has already picked it based on
  // exactly these details (reg. no, type, capacity) and it is in active use.
  // Changing ANY of them here - not just status - would silently invalidate
  // what the Dispatcher is relying on, so the whole form is locked, not just
  // the status field.
  const isLocked = vehicle?.status === 'On Trip'

  const [form, setForm] = useState(
    vehicle
      ? { registrationNo: vehicle.registrationNo, type: vehicle.type, capacity: String(vehicle.capacity), status: vehicle.status }
      : { registrationNo: '', type: '', capacity: '', status: 'Available' }
  )
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoadingVehicle(true)
    setLoadError('')
    vehicleService.listVehicles().then(rows => {
      if (cancelled) return
      const found = rows.find(row => row.id === id)
      setVehicle(found || null)
      if (found) setForm({ registrationNo:found.registrationNo, type:found.type, capacity:String(found.capacity), status:found.status })
      else setLoadError('This vehicle was not found in the registry.')
    }).catch(error => {
      if (!cancelled) { setVehicle(null); setLoadError(error.message || 'Unable to load this vehicle.') }
    }).finally(() => { if (!cancelled) setLoadingVehicle(false) })
    return () => { cancelled = true }
  }, [id,retry])

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  useEffect(() => {
    if (!saved) return undefined
    const timer = window.setTimeout(() => navigate('/admin/vehicles'), 2000)
    return () => window.clearTimeout(timer)
  }, [saved, navigate])

  function validate() {
    const errs = {}
    if (!isRequired(form.registrationNo)) errs.registrationNo = 'Registration number is required.'
    if (!isRequired(form.type)) errs.type = 'Vehicle type is required.'
    if (!isRequired(form.capacity)) errs.capacity = 'Capacity is required.'
    else if (!isValidCapacity(form.capacity)) errs.capacity = CAPACITY_ERROR
    if (!isRequired(form.status)) errs.status = 'Status is required.'
    return errs
  }

  async function handleSubmit(e) {
    e.preventDefault()
    // Defence in depth: even if the form were somehow submitted while
    // locked (e.g. disabled attributes stripped via devtools), nothing is
    // sent to the server for an On Trip vehicle.
    if (isLocked) return

    setFormError(null)
    setSaved(false)

    const errs = validate()
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    setSubmitting(true)
    try {
      await vehicleService.updateVehicle(id, form)
      setSaved(true)
      setFieldErrors({})
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

  if (loadingVehicle) return <AppShell title="Edit Vehicle" dirty={!!vehicle && !saved && !isLocked && Object.entries(form).some(([key,value]) => String(value) !== String(vehicle[key]))}><p role="status">Loading vehicle details...</p></AppShell>

  if (!vehicle) {
    return (
      <AppShell title="Edit Vehicle" dirty={!!vehicle && !saved && !isLocked && Object.entries(form).some(([key,value]) => String(value) !== String(vehicle[key]))}>
        <div className="page-header">
          <div>
            <PageHeading eyebrow="Fleet management" title="Edit Vehicle" />
            <p className="page-header-desc">{loadError || 'Vehicle details are unavailable.'}</p>
          </div>
        </div>
        <div className="card card-pad" style={{ maxWidth: 560 }}>
          <p className="page-status-text">
            Please go back to the Vehicles list and use the edit button on the vehicle you want to update.
          </p>
          <Button variant="secondary" onClick={() => setRetry(x => x+1)}>Retry</Button>
          <Link className="admin-cancel-link" to="/admin/vehicles">Back to Vehicles</Link>
        </div>
      </AppShell>
    )
  }

  return (
    <AppShell title="Edit Vehicle" dirty={!!vehicle && !saved && !isLocked && Object.entries(form).some(([key,value]) => String(value) !== String(vehicle[key]))}>
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Fleet management" title="Edit Vehicle" />
          <p className="page-header-desc">Update details and status for {vehicle.registrationNo}.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{saved ? 'Vehicle updated successfully. Redirecting to the vehicle list…' : null}</SuccessMessage>

          {isLocked && (
            <div
              className="flex items-start gap-2.5 rounded-xl border border-error/30 bg-error-container/60 px-4 py-3 font-body-sm text-body-sm text-on-error-container"
              role="alert"
            >
              This vehicle is currently on a trip. Editing will be available again once the trip is completed or cancelled.
            </div>
          )}

          <FormField label="Registration number" name="registrationNo" value={form.registrationNo} onChange={update('registrationNo')} error={fieldErrors.registrationNo} required autoComplete="off" readOnly={isLocked} />
          <FormField label="Vehicle type" name="type" value={form.type} onChange={update('type')} error={fieldErrors.type} required placeholder="e.g. Van, Truck, Motorbike" readOnly={isLocked} />
          <FormField label="Capacity" name="capacity" value={form.capacity} onChange={update('capacity')} error={fieldErrors.capacity} required placeholder="e.g. 500 kg" hint={CAPACITY_HINT} readOnly={isLocked} />

          <div className="flex flex-col gap-1.5">
            <label className="flex items-center justify-between font-title-md text-body-sm text-on-surface" htmlFor="status">
              <span>Status</span>
            </label>
            <select
              id="status"
              className="select-input"
              value={form.status}
              onChange={update('status')}
              disabled={isLocked}
            >
              {STATUSES.map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
            {fieldErrors.status && (
              <span className="font-body-sm text-body-sm text-error">{fieldErrors.status}</span>
            )}
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            {!isLocked && (
              <Button type="submit" loading={submitting} disabled={saved}>{submitting ? 'Saving…' : 'Save changes'}</Button>
            )}
            <Button variant="secondary" type="button" onClick={() => { if (saved || isLocked || !Object.entries(form).some(([key,value]) => String(value) !== String(vehicle[key])) || window.confirm('Discard your unsaved changes?')) navigate('/admin/vehicles') }}>
              {isLocked ? 'Back to Vehicles' : 'Cancel'}
            </Button>
          </div>
        </form>
      </div>
    </AppShell>
  )
}