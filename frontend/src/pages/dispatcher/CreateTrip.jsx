import { useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'
import { isRequired } from '../../utils/validation'

const EMPTY = { pickupLocation: '', dropoffLocation: '', requiredCapacity: '' }

// US-09 — connects to POST /api/trips on the Trip Dispatch Service.
export default function CreateTrip() {
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
    if (!isRequired(form.pickupLocation)) errs.pickupLocation = 'Pickup location is required.'
    if (!isRequired(form.dropoffLocation)) errs.dropoffLocation = 'Drop-off location is required.'
    if (!isRequired(form.requiredCapacity)) errs.requiredCapacity = 'Required capacity is required.'
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
      await tripService.createTrip(form)
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
    <AppShell title="Create Trip">
      <div className="page-header">
        <div>
          <div className="page-header-title">Create Trip</div>
          <p className="page-header-desc">Request a new trip. It will start in "Requested" status.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{created ? 'Trip created successfully.' : null}</SuccessMessage>

          <FormField label="Pickup location" name="pickupLocation" value={form.pickupLocation} onChange={update('pickupLocation')} error={fieldErrors.pickupLocation} required placeholder="e.g. Kegalle Bus Stand" />
          <FormField label="Drop-off location" name="dropoffLocation" value={form.dropoffLocation} onChange={update('dropoffLocation')} error={fieldErrors.dropoffLocation} required placeholder="e.g. Colombo Fort" />
          <FormField label="Required capacity" name="requiredCapacity" value={form.requiredCapacity} onChange={update('requiredCapacity')} error={fieldErrors.requiredCapacity} required hint="A number with an optional unit, e.g. 500 kg, 8 persons" />

          <FormField label="Status" name="status" value="Requested" readOnly />

          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            <Button type="submit" loading={submitting}>{submitting ? 'Creating…' : 'Create trip'}</Button>
            {created && (
              <>
                <Link to="/dispatcher/trips"><Button variant="secondary" type="button">View Trips</Button></Link>
                <Button variant="ghost" type="button" onClick={() => setCreated(false)}>Create Another Trip</Button>
              </>
            )}
          </div>
        </form>
      </div>
    </AppShell>
  )
}