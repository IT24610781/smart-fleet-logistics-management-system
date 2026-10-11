import { Link } from 'react-router-dom'
import PageHeading from '../../components/common/PageHeading'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTheme } from '../../hooks/useTheme'
import AppShell from '../../components/layout/AppShell'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import ConfirmDialog from '../../components/common/ConfirmDialog'
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
  const { updateUser, logout } = useAuth()
  const navigate = useNavigate()
  function handleLogout() {
    if (submitting) return
    if (dirty && !window.confirm('Discard your unsaved changes and log out?')) return
    logout()
    navigate('/login', { replace: true })
  }

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

  useEffect(() => {
    if (!editing || !dirty) return
    const unload = event => { event.preventDefault(); event.returnValue = '' }
    const leave = event => {
      const anchor = event.target.closest('a[href]')
      if (anchor && !event.defaultPrevented && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0 && !window.confirm('Leave this page and discard your unsaved profile changes?')) {
        event.preventDefault()
        event.stopPropagation()
      }
    }
    window.addEventListener('beforeunload', unload)
    document.addEventListener('click', leave, true)
    return () => { window.removeEventListener('beforeunload', unload); document.removeEventListener('click', leave, true) }
  }, [dirty, editing])

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
        <section className="profile-section"><div className="profile-section-heading"><h2>Security</h2></div><div className="profile-settings-card profile-theme-row"><div><strong>Password</strong><p>Update your password with an email verification code.</p></div><Link to="/profile/password" className="password-back-link">Change password</Link></div></section>
        <ProfilePreferences onLogout={handleLogout} disabled={submitting} />
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
        <section className="profile-section"><div className="profile-section-heading"><h2>Security</h2></div><div className="profile-settings-card profile-theme-row"><div><strong>Password</strong><p>Update your password with an email verification code.</p></div><Link to="/profile/password" className="password-back-link">Change password</Link></div></section>
        <ProfilePreferences onLogout={handleLogout} disabled={submitting} />
      </AppShell>
    )
  }

  const fullName = `${profile.firstName || ''} ${profile.lastName || ''}`.trim() || profile.username
  const memberSince = formatMonthYear(profile.createdAt)
  const personal = [['firstName','First name',profile.firstName],['lastName','Last name',profile.lastName],['contactNumber','Contact number',profile.contactNumber],['email','Email',profile.email]]
  const account = [['username','Username',profile.username],['role','Role',profile.role],['status','Account status',profile.isActive === false ? 'Inactive' : 'Active'],['memberSince','Member since',memberSince]]
  const detailGrid = rows => <dl className="profile-detail-grid">{rows.map(([id,label,value]) => <div key={id}><dt>{label}</dt><dd id={`view-${id}`}>{value || 'Not provided'}</dd></div>)}</dl>
  return (
    <AppShell title="Profile">
      <div className="profile-page">
        <PageHeading eyebrow="Your account" title="Your details" description="Manage your personal information, account preferences and session." />
        <div className="profile-identity"><span className="profile-avatar" aria-hidden="true">{initialsOf(profile)}</span><div><strong id="profile-display-name">{fullName}</strong><span>@{profile.username}</span></div></div>
        <SuccessMessage>{success ? 'Profile updated successfully.' : null}</SuccessMessage>
        <section className="profile-section">
          <div className="profile-section-heading"><h2>Personal information</h2>{!editing && <Button id="edit-profile-button" variant="secondary" onClick={startEditing}>Edit details</Button>}</div>
          {!editing ? detailGrid(personal) : <form className="profile-edit-form" onSubmit={handleSubmit} noValidate onKeyDown={event => { if (event.key === 'Escape' && !submitting) handleCancel() }}>
            <ErrorMessage>{formError}</ErrorMessage>
            <fieldset disabled={submitting}><div className="form-grid-2">
              <FormField label="First name" name="firstName" value={form.firstName} onChange={update('firstName')} error={fieldErrors.firstName} maxLength={NAME_MAX} autoComplete="given-name" required />
              <FormField label="Last name" name="lastName" value={form.lastName} onChange={update('lastName')} error={fieldErrors.lastName} maxLength={NAME_MAX} autoComplete="family-name" required />
              <FormField label="Contact number" name="contactNumber" type="tel" value={form.contactNumber} onChange={update('contactNumber')} error={fieldErrors.contactNumber} maxLength={CONTACT_MAX} autoComplete="tel" required />
              <FormField label="Email" name="email" value={profile.email || ''} readOnly hint="Email cannot be changed here." />
            </div></fieldset>
            <div className="profile-edit-actions"><Button id="cancel-edit-button" variant="secondary" onClick={handleCancel} disabled={submitting}>Cancel</Button><Button type="submit" loading={submitting} disabled={!dirty}>Save changes</Button></div>
          </form>}
        </section>
        <section className="profile-section"><div className="profile-section-heading"><h2>Account information</h2></div>{detailGrid(account)}</section>
        <section className="profile-section"><div className="profile-section-heading"><h2>Security</h2></div><div className="profile-settings-card profile-theme-row"><div><strong>Password</strong><p>Update your password with an email verification code.</p></div><Link to="/profile/password" className="password-back-link">Change password</Link></div></section>
        <ProfilePreferences onLogout={handleLogout} disabled={submitting} />
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

function ProfilePreferences({ onLogout, disabled }) {
  const { theme, setTheme } = useTheme()
  return <section className="profile-section profile-preferences">
    <div className="profile-section-heading"><h2>Preferences</h2></div>
    <div className="profile-settings-card">
      <div className="profile-theme-row"><div><strong>Appearance</strong><p>Choose a light or dark theme for this browser.</p></div><div className="profile-theme-options" role="group" aria-label="Appearance">
        {['light','dark'].map(option => <button key={option} type="button" aria-pressed={theme === option} onClick={() => setTheme(option)}><span className={`theme-swatch theme-swatch-${option}`} />{option === 'light' ? 'Light' : 'Dark'}</button>)}
      </div></div>
      <div className="profile-session-row"><div><strong>Account session</strong><p>Sign out of Smart Fleet on this browser.</p></div><Button variant="danger" onClick={onLogout} disabled={disabled}>Logout</Button></div>
    </div>
  </section>
}
