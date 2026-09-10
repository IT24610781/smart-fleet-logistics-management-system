import { useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as userService from '../../services/userService'
import { isRequired, isValidEmail, isValidPassword, isValidUsername, isValidContactNumber } from '../../utils/validation'

const EMPTY = { username: '', email: '', password: '', firstName: '', lastName: '', contactNumber: '' }

// Priority 2 / US-01 — connects to POST /api/users/drivers.
export default function CreateDriver() {
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
    if (!isRequired(form.username)) errs.username = 'Username is required.'
    else if (!isValidUsername(form.username)) errs.username = 'Username must be 3-20 characters, start with a letter, and use only letters, numbers, "_" or ".".'
    if (!isValidEmail(form.email)) errs.email = 'Enter a valid email address.'
    if (!isValidPassword(form.password)) errs.password = 'Password must be at least 8 characters.'
    if (!isRequired(form.firstName)) errs.firstName = 'First name is required.'
    if (!isRequired(form.lastName)) errs.lastName = 'Last name is required.'
    if (!isValidContactNumber(form.contactNumber)) errs.contactNumber = 'Enter a valid contact number.'
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
      await userService.createDriver(form)
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
    <AppShell title="Create Driver">
      <div className="page-header">
        <div>
          <div className="page-header-title">Create Driver</div>
          <p className="page-header-desc">Set up a new driver account. The driver will receive their login credentials separately.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{created ? 'Driver account created successfully.' : null}</SuccessMessage>

          <div className="form-grid-2">
            <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required />
            <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required />
          </div>

          <FormField label="Username" name="username" value={form.username} onChange={update('username')} error={fieldErrors.username} required autoComplete="off" />
          <FormField label="Email" name="email" type="email" value={form.email} onChange={update('email')} error={fieldErrors.email} required autoComplete="off" />
          <FormField label="Password" name="password" type="password" value={form.password} onChange={update('password')} error={fieldErrors.password} hint="At least 8 characters" required autoComplete="new-password" />
          <FormField label="Contact number" name="contactNumber" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required />

          <FormField label="Role" name="role" value="Driver" readOnly />

          <div style={{ display: 'flex', gap: 'var(--space-3)' }}>
            <Button type="submit" loading={submitting}>{submitting ? 'Creating…' : 'Create driver'}</Button>
            {created && (
              <>
                <Link to="/admin/drivers"><Button variant="secondary" type="button">View Drivers</Button></Link>
                <Button variant="ghost" type="button" onClick={() => setCreated(false)}>Create Another Driver</Button>
              </>
            )}
          </div>
        </form>
      </div>
    </AppShell>
  )
}
