import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as vehicleService from '../../services/vehicleService'
import { isRequired, isValidCapacity } from '../../utils/validation'

const STATUSES = ['Available', 'On Trip', 'Under Maintenance']

// US-06 — Admin updates a vehicle's details and status.
export default function EditVehicle() {
  const { id } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const vehicle = location.state?.vehicle

  const [form, setForm] = useState(
    vehicle
      ? { type: vehicle.type, capacity: String(vehicle.capacity), status: vehicle.status }
      : { type: '', capacity: '', status: 'Available' }
  )
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [saved, setSaved] = useState(false)

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  function validate() {
    const errs = {}
    if (!isRequired(form.type)) errs.type = 'Vehicle type is required.'
    if (!isValidCapacity(form.capacity)) errs.capacity = 'Capacity must be a valid positive number.'
    if (!isRequired(form.status)) errs.status = 'Status is required.'
    return errs
  }

  async function handleSubmit(e) {
    e.preventDefault()
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

  if (!vehicle) {
    return (
      <AppShell title="Edit Vehicle">
        <div className="page-header">
          <div>
            <div className="page-header-title">Edit Vehicle</div>
            <p className="page-header-desc">We couldn't find that vehicle's details.</p>
          </div>
        </div>
        <div className="card card-pad" style={{ maxWidth: 560 }}>
          <p className="page-status-text">
            Please go back to the Vehicles list and use the edit button on the vehicle you want to update.
          </p>
          <Link to="/admin/vehicles"><Button variant="secondary" type="button">Back to Vehicles</Button></Link>
        </div>
      </AppShell>
    )
  }

  return (
    <AppShell title="Edit Vehicle">
      <div className="page-header">
        <div>
          <div className="page-header-title">Edit Vehicle</div>
          <p className="page-header-desc">Update details and status for {vehicle.registrationNo}.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{saved ? 'Vehicle updated successfully.' : null}</SuccessMessage>

          <FormField label="Registration number" name="registrationNo" value={vehicle.registrationNo} readOnly />
          <FormField label="Vehicle type" name="type" value={form.type} onChange={update('type')} error={fieldErrors.type} required placeholder="e.g. Van, Truck, Motorbike" />
          <FormField label="Capacity" name="capacity" value={form.capacity} onChange={update('capacity')} error={fieldErrors.capacity} required hint="Number of passengers or load units" />

          <div className="flex flex-col gap-1.5">
            <label className="flex items-center justify-between font-title-md text-body-sm text-on-surface" htmlFor="status">
              <span>Status</span>
            </label>
            <select
              id="status"
              className="select-input"
              value={form.status}
              onChange={update('status')}
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
            <Button type="submit" loading={submitting}>{submitting ? 'Saving…' : 'Save changes'}</Button>
            <Button variant="secondary" type="button" onClick={() => navigate('/admin/vehicles')}>Cancel</Button>
          </div>
        </form>
      </div>
    </AppShell>
  )
}