import { useState } from 'react'
import { Link } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as userService from '../../services/userService'
import { useToast } from '../../context/ToastContext'
import { isRequired, isValidEmail, isValidPassword, isValidUsername, isValidContactNumber } from '../../utils/validation'

const EMPTY = { username: '', email: '', password: '', firstName: '', lastName: '', contactNumber: '' }

// US-01 — connects to POST /api/users/drivers. Form logic unchanged from
// the original implementation; markup follows the Stitch
// "admin_create_driver_us_01" screen.
export default function CreateDriver() {
  const { showToast } = useToast()
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
      showToast('Driver account created successfully.', 'success')
    } catch (err) {
      if (err.fieldErrors) {
        setFieldErrors((f) => ({ ...f, ...err.fieldErrors }))
        setFormError('Please fix the highlighted fields.')
      } else {
        setFormError(err.message || 'Something went wrong. Please try again.')
        showToast(err.message || 'Unable to create the driver account.', 'error')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AppShell title="Create Driver">
      <div className="mb-6">
        <h2 className="font-headline-lg text-headline-lg text-on-surface">Create Driver</h2>
        <p className="font-body-md text-body-md text-on-surface-variant mt-1">
          Set up a new driver account. The driver will receive their login credentials separately.
        </p>
      </div>

      <div className="w-full max-w-2xl mx-auto rounded-xl border border-outline-variant bg-surface-container-lowest p-6">
        <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{created ? 'Driver account created successfully.' : null}</SuccessMessage>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required />
            <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required />
          </div>

          <FormField label="Username" name="username" value={form.username} onChange={update('username')} error={fieldErrors.username} required autoComplete="off" />
          <FormField label="Email" name="email" type="email" value={form.email} onChange={update('email')} error={fieldErrors.email} required autoComplete="off" />
          <FormField label="Password" name="password" type="password" value={form.password} onChange={update('password')} error={fieldErrors.password} hint="At least 8 characters" required autoComplete="new-password" />
          <FormField label="Contact number" name="contactNumber" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required />

          <FormField label="Role" name="role" value="Driver" readOnly />

          <div className="flex flex-wrap gap-3 pt-2">
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
