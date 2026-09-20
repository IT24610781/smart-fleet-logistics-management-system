import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import * as authService from '../../services/authService'
import { useToast } from '../../context/ToastContext'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import AuthShell from '../../components/layout/AuthShell'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { isRequired, isValidEmail, isValidPassword, isValidUsername, isValidContactNumber } from '../../utils/validation'

const EMPTY = {
  firstName: '', lastName: '', username: '', email: '',
  password: '', confirmPassword: '', contactNumber: '',
}

// US-08: Dispatcher creates their own account via
// POST /api/auth/register/dispatcher. Client-side checks run first; any
// field-level errors returned by the API (400) are shown under the matching
// input, and a duplicate username/email (409) is shown as a form error.
export default function DispatcherRegister() {
  const { showToast } = useToast()
  const navigate = useNavigate()
  const [form, setForm] = useState(EMPTY)
  const [fieldErrors, setFieldErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)

  // US-08 Scenario 4: after a successful registration, show the confirmation
  // for a moment, then send the person to the login page. Done in an effect
  // (with window.setTimeout, like ToastContext) so the timer is cancelled if
  // they leave the page first.
  useEffect(() => {
    if (!success) return undefined
    const timer = window.setTimeout(() => navigate('/login'), 2000)
    return () => window.clearTimeout(timer)
  }, [success, navigate])

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  function validate() {
    const errs = {}
    if (!isRequired(form.firstName)) errs.firstName = 'First name is required.'
    if (!isRequired(form.lastName)) errs.lastName = 'Last name is required.'
    if (!isRequired(form.username)) errs.username = 'Username is required.'
    else if (!isValidUsername(form.username)) errs.username = 'Username must be 3-20 characters, start with a letter, and use only letters, numbers, "_" or ".".'
    if (!isValidEmail(form.email)) errs.email = 'Enter a valid email address.'
    if (!isValidPassword(form.password)) errs.password = 'Password must be at least 8 characters.'
    if (form.confirmPassword !== form.password) errs.confirmPassword = 'Passwords do not match.'
    if (!isValidContactNumber(form.contactNumber)) errs.contactNumber = 'Enter a valid contact number.'
    return errs
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setSuccess(false)

    const errs = validate()
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    setSubmitting(true)
    try {
      await authService.registerDispatcher({
        firstName: form.firstName,
        lastName: form.lastName,
        username: form.username,
        email: form.email,
        password: form.password,
        contactNumber: form.contactNumber,
      })
      setSuccess(true)
      setForm(EMPTY)
      showToast('Account created. You can now log in.', 'success')
    } catch (err) {
      setFormError(err.fieldErrors ? null : err.message)
      if (!err.fieldErrors) showToast(err.message || 'Unable to create the account.', 'error')
      if (err.fieldErrors) setFieldErrors((f) => ({ ...f, ...err.fieldErrors }))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <h2 className="font-headline-lg text-headline-lg text-on-surface mb-1">Create your account</h2>
      <p className="font-body-md text-body-md text-on-surface-variant mb-6">
        Register as a Dispatcher to create trips and assign vehicles.
      </p>

      <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <ErrorMessage>{formError}</ErrorMessage>
        <SuccessMessage>{success ? 'Account created successfully. Redirecting you to the login page…' : null}</SuccessMessage>

        <div className="form-grid-2">
          <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required autoComplete="given-name" />
          <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required autoComplete="family-name" />
        </div>
        <FormField label="Username" name="username" value={form.username} onChange={update('username')} error={fieldErrors.username} hint="3-20 characters, starts with a letter" required autoComplete="username" />
        <FormField label="Email" name="email" type="email" value={form.email} onChange={update('email')} error={fieldErrors.email} required autoComplete="email" />
        <div className="form-grid-2">
          <FormField label="Password" name="password" type="password" value={form.password} onChange={update('password')} error={fieldErrors.password} hint="At least 8 characters" required autoComplete="new-password" />
          <FormField label="Confirm password" name="confirmPassword" type="password" value={form.confirmPassword} onChange={update('confirmPassword')} error={fieldErrors.confirmPassword} required autoComplete="new-password" />
        </div>
        <div className="form-grid-2">
          <FormField label="Contact number" name="contactNumber" type="tel" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required autoComplete="tel" />
          <FormField label="Role" name="role" value="Dispatcher" readOnly />
        </div>

        <Button type="submit" block loading={submitting}>
          {submitting ? 'Creating account…' : 'Create account'}
        </Button>
      </form>

      <div className="mt-6 text-center font-body-sm text-body-sm text-on-surface-variant">
        Already have an account?{' '}
        <Link to="/login" className="text-primary font-semibold hover:underline">
          Log in
        </Link>
      </div>
    </AuthShell>
  )
}
