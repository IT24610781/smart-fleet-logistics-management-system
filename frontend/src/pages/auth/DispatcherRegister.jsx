import { useState } from 'react'
import { Link } from 'react-router-dom'
import * as authService from '../../services/authService'
import { useToast } from '../../context/ToastContext'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { isRequired, isValidEmail, isValidPassword, isValidUsername, isValidContactNumber } from '../../utils/validation'
import './AuthLayout.css'

const EMPTY = {
  firstName: '', lastName: '', username: '', email: '',
  password: '', confirmPassword: '', contactNumber: '',
}

// Section 15 — UI architecture only. The registration endpoint is not live
// yet (planned for a later sprint), so submission surfaces that plainly
// instead of pretending the account was created.
export default function DispatcherRegister() {
  const { showToast } = useToast()
  const [form, setForm] = useState(EMPTY)
  const [fieldErrors, setFieldErrors] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)

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
      showToast('Dispatcher account request submitted.', 'success')
    } catch (err) {
      // Backend registration endpoint does not exist yet — say so clearly
      // rather than showing a generic or misleading error.
      if (err.status === 404) {
        setFormError('Dispatcher registration is not available yet. This screen is ready for when the backend adds it.')
        showToast('Dispatcher registration is not available yet.', 'error')
      } else {
        setFormError(err.fieldErrors ? null : err.message)
        if (!err.fieldErrors) showToast(err.message || 'Unable to create the account.', 'error')
        if (err.fieldErrors) setFieldErrors((f) => ({ ...f, ...err.fieldErrors }))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card wide">
        <div className="auth-brand">
          <img src="/fleet-mark.svg" alt="" />
          <h1>Create Dispatcher Account</h1>
          <p>Registration is limited to the Dispatcher role</p>
        </div>

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{success ? 'Account request submitted.' : null}</SuccessMessage>

          <div className="form-grid-2">
            <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required />
            <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required />
          </div>
          <FormField label="Username" name="username" value={form.username} onChange={update('username')} error={fieldErrors.username} required />
          <FormField label="Email" name="email" type="email" value={form.email} onChange={update('email')} error={fieldErrors.email} required autoComplete="email" />
          <div className="form-grid-2">
            <FormField label="Password" name="password" type="password" value={form.password} onChange={update('password')} error={fieldErrors.password} hint="At least 8 characters" required autoComplete="new-password" />
            <FormField label="Confirm password" name="confirmPassword" type="password" value={form.confirmPassword} onChange={update('confirmPassword')} error={fieldErrors.confirmPassword} required autoComplete="new-password" />
          </div>
          <FormField label="Contact number" name="contactNumber" type="tel" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required />

          <FormField label="Role" name="role" value="Dispatcher" readOnly />

          <Button type="submit" block loading={submitting}>
            {submitting ? 'Creating account…' : 'Create account'}
          </Button>
        </form>

        <div className="auth-foot">
          Already have an account? <Link to="/login">Log in</Link>
        </div>
      </div>
    </div>
  )
}
