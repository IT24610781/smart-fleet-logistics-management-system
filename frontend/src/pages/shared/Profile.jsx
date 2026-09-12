import { useEffect, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useAuth } from '../../context/AuthContext'
import * as userService from '../../services/userService'
import { isRequired, isValidPassword, isValidContactNumber } from '../../utils/validation'

// US-04 — GET/PUT /api/users/me. Form logic unchanged from the original
// implementation; markup follows the Stitch "user_profile_us_04" screen.
export default function Profile() {
  const { user, updateUser } = useAuth()

  const [form, setForm] = useState({
    firstName: '',
    lastName: '',
    contactNumber: '',
    newPassword: '',
    confirmPassword: '',
  })

  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)

  useEffect(() => {
    async function loadProfile() {
      setLoading(true)
      setFormError(null)
      try {
        const profile = await userService.getProfile()
        setForm({
          firstName: profile.firstName || '',
          lastName: profile.lastName || '',
          contactNumber: profile.contactNumber || '',
          newPassword: '',
          confirmPassword: '',
        })
        updateUser({
          firstName: profile.firstName,
          lastName: profile.lastName,
          contactNumber: profile.contactNumber,
          email: profile.email,
          username: profile.username,
          role: profile.role,
          id: profile.id,
        })
      } catch (err) {
        setFormError(err.message || 'Unable to load your profile.')
      } finally {
        setLoading(false)
      }
    }
    loadProfile()
  }, [updateUser])

  function update(field) {
    return (e) => {
      setForm((current) => ({ ...current, [field]: e.target.value }))
      setFieldErrors((current) => ({ ...current, [field]: undefined }))
      setSuccess(false)
    }
  }

  function validate() {
    const errors = {}
    if (!isRequired(form.firstName)) errors.firstName = 'First name is required.'
    if (!isRequired(form.lastName)) errors.lastName = 'Last name is required.'
    if (!isValidContactNumber(form.contactNumber)) errors.contactNumber = 'Enter a valid contact number.'
    if (form.newPassword && !isValidPassword(form.newPassword)) {
      errors.newPassword = 'Password must be at least 8 characters.'
    }
    if (form.newPassword && form.newPassword !== form.confirmPassword) {
      errors.confirmPassword = 'Passwords do not match.'
    }
    return errors
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setFormError(null)
    setSuccess(false)
    setFieldErrors({})

    const errors = validate()
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }

    setSubmitting(true)
    try {
      const payload = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        contactNumber: form.contactNumber.trim(),
      }
      if (form.newPassword) {
        payload.password = form.newPassword
      }

      const updatedProfile = await userService.updateProfile(payload)

      updateUser({
        id: updatedProfile.id,
        username: updatedProfile.username,
        email: updatedProfile.email,
        firstName: updatedProfile.firstName,
        lastName: updatedProfile.lastName,
        contactNumber: updatedProfile.contactNumber,
        role: updatedProfile.role,
      })

      setForm({
        firstName: updatedProfile.firstName || '',
        lastName: updatedProfile.lastName || '',
        contactNumber: updatedProfile.contactNumber || '',
        newPassword: '',
        confirmPassword: '',
      })
      setSuccess(true)
    } catch (err) {
      if (err.fieldErrors) {
        setFieldErrors(err.fieldErrors)
        setFormError('Please fix the highlighted fields.')
      } else {
        setFormError(err.message || 'Unable to update your profile. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) {
    return (
      <AppShell title="Profile">
        <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-6">
          <LoadingSpinner center label="Loading profile…" />
        </div>
      </AppShell>
    )
  }

  return (
    <AppShell title="Profile">
      <div className="mb-6">
        <div className="flex items-center gap-2 font-label-sm text-label-sm text-on-surface-variant mb-1">
          <span className="material-symbols-outlined text-base">account_box</span>
          US-04 · Your account
        </div>
        <h2 className="font-headline-lg text-headline-lg text-on-surface">Profile</h2>
        <p className="font-body-md text-body-md text-on-surface-variant mt-1">View and update your account details.</p>
      </div>

      <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-6 max-w-2xl">
        <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
          <ErrorMessage>{formError}</ErrorMessage>
          <SuccessMessage>{success ? 'Profile updated successfully.' : null}</SuccessMessage>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} required />
            <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} required />
          </div>

          <FormField label="Username" name="username" value={user?.username || ''} readOnly />
          <FormField label="Email" name="email" value={user?.email || ''} readOnly hint="Email cannot be changed" />
          <FormField label="Contact number" name="contactNumber" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} required />
          <FormField label="Role" name="role" value={user?.role || ''} readOnly />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField
              label="New password"
              name="newPassword"
              type="password"
              value={form.newPassword}
              onChange={update('newPassword')}
              error={fieldErrors.newPassword}
              hint="Leave blank to keep current password"
              autoComplete="new-password"
            />
            <FormField
              label="Confirm password"
              name="confirmPassword"
              type="password"
              value={form.confirmPassword}
              onChange={update('confirmPassword')}
              error={fieldErrors.confirmPassword}
              autoComplete="new-password"
            />
          </div>

          <div className="pt-2">
            <Button type="submit" loading={submitting}>{submitting ? 'Saving...' : 'Save changes'}</Button>
          </div>
        </form>
      </div>
    </AppShell>
  )
}
