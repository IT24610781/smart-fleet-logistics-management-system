import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { requestPasswordCode, completePasswordChange } from '../../services/authService'
import FormField from './FormField'
import Button from '../common/Button'
import { ErrorMessage, SuccessMessage } from '../common/ErrorMessage'

export default function PasswordVerificationForm({ change = false }) {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [currentPassword, setCurrentPassword] = useState('')
  const [challenge, setChallenge] = useState(null)
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [done, setDone] = useState(false)
  const [cooldown, setCooldown] = useState(0)
  useEffect(() => {
    if (!challenge) return
    const tick = () => setCooldown(Math.max(0, Math.ceil((challenge.resendAt - Date.now()) / 1000)))
    tick()
    const timer = window.setInterval(tick, 1000)
    return () => window.clearInterval(timer)
  }, [challenge])

  async function send(event) {
    event?.preventDefault()
    if (busy || cooldown > 0) return
    setError(null)
    setBusy(true)
    try {
      const result = await requestPasswordCode(change, { email: email.trim(), currentPassword })
      setChallenge({ ...result, resendAt: Date.now() + result.resendAfterSeconds * 1000 })
      setCode('')
    } catch (err) { setError(err.message || 'Unable to request a code.') }
    finally { setBusy(false) }
  }

  async function complete(event) {
    event.preventDefault()
    if (busy) return
    setError(null)
    if (!/^\d{6}$/.test(code)) return setError('Enter the six-digit code from your email.')
    if (password.length < 8 || new window.TextEncoder().encode(password).length > 72)
      return setError('Use at least 8 characters and no more than 72 UTF-8 bytes.')
    if (password !== confirm) return setError('Passwords do not match.')
    setBusy(true)
    try {
      await completePasswordChange(change, { challengeId: challenge.challengeId, code, newPassword: password, confirmPassword: confirm })
      setCurrentPassword(''); setCode(''); setPassword(''); setConfirm('')
      logout()
      if (change) navigate('/login?passwordChanged=1', { replace: true })
      else setDone(true)
    } catch (err) { setError(err.message || 'Unable to update the password.') }
    finally { setBusy(false) }
  }

  if (done) return <div className="password-verification"><SuccessMessage>Password updated successfully. Sign in with your new password.</SuccessMessage><Link to="/login" className="password-back-link">Back to sign in</Link></div>
  return <div className="password-verification">
    <ErrorMessage>{error}</ErrorMessage>
    {!challenge ? <form onSubmit={send} className="stack">
      <fieldset disabled={busy}>
        {change ? <FormField label="Current password" name="currentPassword" type="password" value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} autoComplete="current-password" required />
          : <FormField label="Registered email" name="recoveryEmail" type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" maxLength={254} required />}
      </fieldset>
      <Button type="submit" block loading={busy}>Send verification code</Button>
    </form> : <>
      <div className="password-code-notice" role="status">
        <strong>{challenge.maskedEmail}</strong>
        <p>{change ? 'Check your inbox for a six-digit verification code.' : challenge.message}</p>
        <p>The code expires in 10 minutes. Check your spam folder too.</p>
      </div>
      <form onSubmit={complete} className="stack">
        <fieldset disabled={busy} className="stack">
          <FormField label="Verification code" name="verificationCode" value={code} onChange={e => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="one-time-code" maxLength={6} required />
          <FormField label="New password" name="newPassword" type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password" hint="At least 8 characters. A long, unique passphrase is recommended." required />
          <FormField label="Confirm new password" name="confirmNewPassword" type="password" value={confirm} onChange={e => setConfirm(e.target.value)} autoComplete="new-password" required />
        </fieldset>
        <Button type="submit" block loading={busy}>Update password</Button>
      </form>
      <div className="password-resend"><Button variant="ghost" onClick={send} disabled={busy || cooldown > 0}>{cooldown ? `Resend code in ${cooldown}s` : 'Resend code'}</Button><Button variant="ghost" onClick={() => { setChallenge(null); setCode(''); setPassword(''); setConfirm('') }} disabled={busy}>{change ? 'Start again' : 'Use a different email'}</Button></div>
    </>}
    <p className="password-session-note">Changing your password ends existing sessions. You will need to sign in again.</p>
    <Link to={change ? '/profile' : '/login'} className="password-back-link">{change ? 'Back to profile' : 'Back to sign in'}</Link>
  </div>
}
