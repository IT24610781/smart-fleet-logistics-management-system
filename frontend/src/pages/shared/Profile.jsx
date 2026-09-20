import { useEffect, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import Icon from '../../components/common/Icon'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useAuth } from '../../context/AuthContext'
import * as userService from '../../services/userService'
import { isRequired, isValidContactNumber } from '../../utils/validation'

// Limits mirror database/auth-user/schema.sql (first/last name VARCHAR(100),
// contact number VARCHAR(20)).
const NAME_MAX = 100
const CONTACT_MAX = 20

function formFrom(profile) {
  return {
    firstName: profile?.firstName || '',
    lastName: profile?.lastName || '',
    contactNumber: profile?.contactNumber || '',
  }
}

function initialsOf(profile) {
  const first = profile?.firstName?.[0] || profile?.username?.[0] || '?'
  const last = profile?.lastName?.[0] || ''
  return `${first}${last}`.toUpperCase()
}

function formatMonthYear(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return date.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })
}

export default function Profile() {
  const { updateUser } = useAuth()

  const [profile, setProfile] = useState(null) // last saved copy from the server
  const [form, setForm] = useState(formFrom(null))
  const [editing, setEditing] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [submitting, setSubmitting] = useState(false)
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)

  useEffect(() => {
    let cancelled = false

    async function loadProfile() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await userService.getProfile()
        if (cancelled) return
        setProfile(data)
        updateUser({
          firstName: data.firstName,
          lastName: data.lastName,
          contactNumber: data.contactNumber,
          email: data.email,
          username: data.username,
          role: data.role,
          id: data.id,
        })
      } catch (err) {
        if (!cancelled) setLoadError(err.message || 'Unable to load your profile.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    loadProfile()
    return () => {
      cancelled = true
    }
  }, [updateUser, reloadKey])

  // Move keyboard focus into the form when editing starts.
  useEffect(() => {
    if (editing) document.getElementById('firstName')?.focus()
  }, [editing])

  const dirty =
    !!profile &&
    (form.firstName.trim() !== (profile.firstName || '') ||
      form.lastName.trim() !== (profile.lastName || '') ||
      form.contactNumber.trim() !== (profile.contactNumber || ''))

  function startEditing() {
    setForm(formFrom(profile))
    setFieldErrors({})
    setFormError(null)
    setSuccess(false)
    setEditing(true)
  }

  function stopEditing() {
    setEditing(false)
    setConfirmDiscard(false)
    setFieldErrors({})
    setFormError(null)
  }

  function handleCancel() {
    if (dirty) setConfirmDiscard(true)
    else stopEditing()
  }

  function update(field) {
    return (e) => {
      setForm((current) => ({ ...current, [field]: e.target.value }))
      setFieldErrors((current) => ({ ...current, [field]: undefined }))
    }
  }

  function validate() {
    const errors = {}
    if (!isRequired(form.firstName)) errors.firstName = 'First name is required.'
    else if (form.firstName.trim().length > NAME_MAX) errors.firstName = `First name must be ${NAME_MAX} characters or fewer.`
    if (!isRequired(form.lastName)) errors.lastName = 'Last name is required.'
    else if (form.lastName.trim().length > NAME_MAX) errors.lastName = `Last name must be ${NAME_MAX} characters or fewer.`
    if (!isValidContactNumber(form.contactNumber) || form.contactNumber.trim().length > CONTACT_MAX) {
      errors.contactNumber = 'Enter a valid contact number.'
    }
    return errors
  }

  async function handleSubmit(e) {
    e.preventDefault()
    if (submitting) return
    setFormError(null)
    setSuccess(false)

    const errors = validate()
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    setSubmitting(true)
    try {
      const updated = await userService.updateProfile({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        contactNumber: form.contactNumber.trim(),
      })

      setProfile((current) => ({ ...current, ...updated }))
      updateUser({
        id: updated.id,
        username: updated.username,
        email: updated.email,
        firstName: updated.firstName,
        lastName: updated.lastName,
        contactNumber: updated.contactNumber,
        role: updated.role,
      })
      setEditing(false)
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
        <div className="max-w-6xl" aria-busy="true">
          <div className="mb-6 h-14 w-64 animate-pulse rounded-xl bg-surface-container" />
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
            <div className="h-80 animate-pulse rounded-xl bg-surface-container" />
            <div className="h-96 animate-pulse rounded-xl bg-surface-container lg:col-span-2" />
          </div>
        </div>
      </AppShell>
    )
  }

  if (!profile) {
    return (
      <AppShell title="Profile">
        <div className="max-w-xl rounded-xl border border-outline-variant bg-surface-container-lowest p-6">
          <ErrorMessage>{loadError || 'Unable to load your profile.'}</ErrorMessage>
          <div className="mt-4">
            <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>
              Try again
            </Button>
          </div>
        </div>
      </AppShell>
    )
  }

  const fullName = `${profile.firstName || ''} ${profile.lastName || ''}`.trim() || profile.username
  const memberSince = formatMonthYear(profile.createdAt)
  const isActive = profile.isActive !== false

  const details = [
    { id: 'firstName', label: 'First name', value: profile.firstName },
    { id: 'lastName', label: 'Last name', value: profile.lastName },
    { id: 'username', label: 'Username', value: profile.username },
    { id: 'email', label: 'Email', value: profile.email },
    { id: 'contactNumber', label: 'Contact number', value: profile.contactNumber },
    { id: 'role', label: 'Role', value: profile.role },
  ]

  return (
    <AppShell title="Profile">
      <div className="max-w-6xl">
        <div className="mb-6">
          <h2 className="font-headline-lg text-headline-lg text-on-surface">My profile</h2>
          <p className="mt-1 font-body-md text-body-md text-on-surface-variant">
            Review and update your personal information.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* Identity card */}
          <aside className="self-start overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest">
            <div className="relative h-24 overflow-hidden bg-primary">
              <div className="pointer-events-none absolute -right-10 -top-14 h-40 w-40 rounded-full bg-secondary/25 blur-2xl" />
              <div className="pointer-events-none absolute -bottom-16 -left-8 h-36 w-36 rounded-full bg-tertiary-container/40 blur-2xl" />
            </div>
            <div className="-mt-10 flex flex-col items-center px-6 pb-6 text-center">
              <div
                className="relative z-10 flex h-20 w-20 items-center justify-center rounded-full bg-secondary font-headline-sm text-headline-sm text-on-secondary ring-4 ring-surface-container-lowest"
                aria-hidden="true"
              >
                {initialsOf(profile)}
              </div>
              <h3 id="profile-display-name" className="mt-3 font-headline-sm text-headline-sm text-on-surface">
                {fullName}
              </h3>
              <p className="font-body-sm text-body-sm text-on-surface-variant">@{profile.username}</p>

              <div className="mt-4 flex flex-wrap items-center justify-center gap-2">
                <span className="inline-flex items-center rounded-full bg-primary-fixed px-2.5 py-1 font-label-md text-label-md text-on-primary-fixed">
                  {profile.role}
                </span>
                <span
                  className={`inline-flex items-center rounded-full px-2.5 py-1 font-label-md text-label-md ${
                    isActive ? 'bg-secondary-fixed text-on-secondary-fixed' : 'bg-error-container text-on-error-container'
                  }`}
                >
                  <span aria-hidden="true" className="mr-1.5 h-1.5 w-1.5 rounded-full bg-current" />
                  {isActive ? 'Active' : 'Inactive'}
                </span>
              </div>

              {memberSince && (
                <p className="mt-5 w-full border-t border-outline-variant/60 pt-4 font-body-sm text-body-sm text-on-surface-variant">
                  Member since {memberSince}
                </p>
              )}
            </div>
          </aside>

          {/* Details card */}
          <section className="overflow-hidden rounded-xl border border-outline-variant bg-surface-container-lowest lg:col-span-2">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-outline-variant px-6 py-5">
              <div>
                <h3 className="font-headline-sm text-headline-sm text-on-surface">Personal information</h3>
                <p className="mt-0.5 font-body-sm text-body-sm text-on-surface-variant">
                  {editing ? 'Update your details, then save your changes.' : 'Your details as they appear in Smart Fleet.'}
                </p>
              </div>
              {!editing && (
                <Button id="edit-profile-button" variant="secondary" onClick={startEditing}>
                  <Icon name="edit" size={16} />
                  Edit details
                </Button>
              )}
            </div>

            {!editing ? (
              <div className="px-6 py-2">
                {success && (
                  <div className="py-4">
                    <SuccessMessage>Profile updated successfully.</SuccessMessage>
                  </div>
                )}
                <dl className="divide-y divide-outline-variant/60">
                  {details.map((row) => (
                    <div key={row.id} className="grid grid-cols-1 gap-1 py-4 sm:grid-cols-3 sm:gap-4">
                      <dt className="font-label-md text-label-md text-on-surface-variant">{row.label}</dt>
                      <dd id={`view-${row.id}`} className="break-words font-body-md text-body-md text-on-surface sm:col-span-2">
                        {row.value || '—'}
                      </dd>
                    </div>
                  ))}
                </dl>
              </div>
            ) : (
              <form
                onSubmit={handleSubmit}
                noValidate
                onKeyDown={(e) => {
                  if (e.key === 'Escape') handleCancel()
                }}
              >
                <div className="flex flex-col gap-5 px-6 py-6">
                  <ErrorMessage>{formError}</ErrorMessage>

                  <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                    <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} maxLength={NAME_MAX} autoComplete="given-name" required />
                    <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} maxLength={NAME_MAX} autoComplete="family-name" required />
                  </div>
                  <FormField label="Contact number" name="contactNumber" type="tel" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} maxLength={CONTACT_MAX} autoComplete="tel" required />

                  <div className="border-t border-outline-variant/60 pt-5">
                    <p className="mb-3 font-label-md text-label-md text-on-surface-variant">
                      Account details - these can't be changed here
                    </p>
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <FormField label="Username" name="username" value={profile.username || ''} readOnly />
                      <FormField label="Role" name="role" value={profile.role || ''} readOnly />
                    </div>
                    <div className="mt-4">
                      <FormField label="Email" name="email" value={profile.email || ''} readOnly hint="Email cannot be changed" />
                    </div>
                  </div>
                </div>

                <div className="flex flex-col-reverse gap-3 border-t border-outline-variant bg-surface-container-low/60 px-6 py-4 sm:flex-row sm:justify-end">
                  <Button id="cancel-edit-button" variant="secondary" onClick={handleCancel} disabled={submitting}>
                    Cancel
                  </Button>
                  <Button type="submit" loading={submitting} disabled={!dirty}>
                    {submitting ? 'Saving…' : 'Save changes'}
                  </Button>
                </div>
              </form>
            )}
          </section>
        </div>
      </div>

      <ConfirmDialog
        open={confirmDiscard}
        title="Discard your changes?"
        body="You have unsaved changes. If you leave now, they will be lost."
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        onConfirm={stopEditing}
        onCancel={() => setConfirmDiscard(false)}
      />
    </AppShell>
  )
}
