import { useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useAuth } from '../../context/AuthContext'
import * as userService from '../../services/userService'
import { isRequired, isValidPassword, isValidContactNumber } from '../../utils/validation'

// Priority 4 / US-04 — shared across all three roles instead of duplicated
// per-role Profile.jsx files, per the reusability guidance in section 27.
// Editing calls userService.updateProfile(), which is a placeholder until
// PUT /api/users/me exists on the backend.
export default function Profile() {
  const { user, updateUser } = useAuth()
  const [form, setForm] = useState({
    firstName: user?.firstName || '',
    lastName: user?.lastName || '',
    contactNumber: user?.contactNumber || '',
    newPassword: '',
    confirmPassword: '',
  })
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  function validate() {
    const errs = {}
    if (!isRequired(form.firstName)) errs.firstName = 'First name is required.'
    if (!isRequired(form.lastName)) errs.lastName = 'Last name is required.'
    if (!isValidContactNumber(form.contactNumber)) errs.contactNumber = 'Enter a valid contact number.'
    if (form.newPassword && !isValidPassword(form.newPassword)) errs.newPassword = 'Password must be at least 8 characters.'
    if (form.newPassword && form.newPassword !== form.confirmPassword) errs.confirmPassword = 'Passwords do not match.'
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
      // safeFields is what may ever be persisted (AuthContext + localStorage).
      // apiPayload additionally carries the new password, but only over the
      // wire to the backend — it is never merged into user state below.
      const safeFields = {
        firstName: form.firstName,
        lastName: form.lastName,
        contactNumber: form.contactNumber,
      }
      const apiPayload = {
        ...safeFields,
        ...(form.newPassword ? { password: form.newPassword } : {}),
      }
      await userService.updateProfile(apiPayload)
      updateUser(safeFields)
      setSuccess(true)
      setForm((f) => ({ ...f, newPassword: '', confirmPassword: '' }))
    } catch (err) {
      if (err.status === 404) {
        setFormError('Profile updates aren\u2019t connected to the backend yet. This form is ready for when the endpoint is live.')
      } else if (err.fieldErrors) {
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
    <AppShell title="Profile">
      <div className="page-header">
        <div>
          <div className="page-header-title">Profile</div>
          <p className="page-header-desc">View and update your account details.</p>
        </div>
      </div>

      <div className="card card-pad" style={{ maxWidth: 560 }}>
        <form className="stack" onSubmit={handleSubmit} noValidate style={{ gap: 'var(--space-4)' }}>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{success ? 'Profile updated successfully.' : null}</SuccessMessage>

          <div className="form-grid-2">
            <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required />
            <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required />
          </div>

          <FormField label="Username" name="username" value={user?.username || ''} readOnly />
          <FormField label="Email" name="email" value={user?.email || ''} readOnly hint="Email cannot be changed" />
          <FormField label="Contact number" name="contactNumber" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required />
          <FormField label="Role" name="role" value={user?.role || ''} readOnly />

          <div className="form-grid-2">
            <FormField label="New password" name="newPassword" type="password" value={form.newPassword} onChange={update('newPassword')} error={fieldErrors.newPassword} hint="Leave blank to keep current password" autoComplete="new-password" />
            <FormField label="Confirm password" name="confirmPassword" type="password" value={form.confirmPassword} onChange={update('confirmPassword')} error={fieldErrors.confirmPassword} autoComplete="new-password" />
          </div>

          <div>
            <Button type="submit" loading={submitting}>{submitting ? 'Saving…' : 'Save changes'}</Button>
          </div>
        </form>
      </div>
    </AppShell>
  )
}
