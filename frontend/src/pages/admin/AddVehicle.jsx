import PageHeading from '../../components/common/PageHeading'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AdminShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as vehicleService from '../../services/vehicleService'
import { isRequired, isValidCapacity, CAPACITY_HINT, CAPACITY_ERROR } from '../../utils/validation'

const EMPTY = { registrationNo: '', type: '', capacity: '' }

// US-04 — connects to POST /api/vehicles.
export default function AddVehicle() {
  const [form, setForm] = useState(EMPTY)
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [created, setCreated] = useState(false)

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  function validate() {
    const errs = {}
    if (!isRequired(form.registrationNo)) errs.registrationNo = 'Registration number is required.'
    if (!isRequired(form.type)) errs.type = 'Vehicle type is required.'
    if (!isRequired(form.capacity)) errs.capacity = 'Capacity is required.'
    else if (!isValidCapacity(form.capacity)) errs.capacity = CAPACITY_ERROR
    return errs
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setCreated(false)

    const errs = validate()
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    setSubmitting(true)
    try {
      await vehicleService.createVehicle(form)
      setCreated(true)
      setForm(EMPTY)
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

  return (
    <AppShell dirty={Object.values(form).some(value => String(value).trim()) && !created} title="Add Vehicle">
      <div className="page-header">
        <div>
          <PageHeading eyebrow="Fleet management" title="Add Vehicle" />
          <p className="page-header-desc">Register a new vehicle. It will default to "Available" status.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{created ? 'Vehicle added successfully.' : null}</SuccessMessage>

          <FormField label="Registration number" name="registrationNo" value={form.registrationNo} onChange={update('registrationNo')} error={fieldErrors.registrationNo} required autoComplete="off" />
          <FormField label="Vehicle type" name="type" value={form.type} onChange={update('type')} error={fieldErrors.type} required placeholder="e.g. Van, Truck, Motorbike" />
          <FormField label="Capacity" name="capacity" value={form.capacity} onChange={update('capacity')} error={fieldErrors.capacity} required placeholder="e.g. 500 kg" hint={CAPACITY_HINT} />

          <FormField label="Status" name="status" value="Available" readOnly />

          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            <Button type="submit" loading={submitting}>{submitting ? 'Adding…' : 'Add vehicle'}</Button>
            <Link className="admin-cancel-link" to="/admin/vehicles">Cancel</Link>
            {created && (
              <>
                <Link className="admin-cancel-link" to="/admin/vehicles">View Vehicles</Link>
                <Button variant="ghost" type="button" onClick={() => setCreated(false)}>Add Another Vehicle</Button>
              </>
            )}
          </div>
        </form>
      </div>
    </AppShell>
  )
}