import { useState } from 'react'
import { useNavigate, useSearchParams, Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useToast } from '../../context/ToastContext'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import AuthShell from '../../components/layout/AuthShell'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'

const HOME_BY_ROLE = {
  Admin: '/admin/dashboard',
  Dispatcher: '/dispatcher/dashboard',
  Driver: '/driver/dashboard',
}

// US-02 — connects to POST /api/auth/login. Layout follows the Stitch
// "login_page_us_02" screen (split brand panel + auth card); all form
// logic below is unchanged from the original implementation.
export default function Login() {
  const { login } = useAuth()
  const { showToast } = useToast()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState(null)

  async function handleSubmit(e) {
    e.preventDefault()
    setError(null)

    if (!username.trim() || !password) {
      setError('Enter your username and password.')
      return
    }

    setSubmitting(true)
    try {
      const user = await login(username.trim(), password)
      showToast('Signed in successfully.', 'success')
      const redirectTo = HOME_BY_ROLE[user.role] || '/login'
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setError(err.message || 'Invalid username or password.')
      showToast(err.message || 'Unable to sign in.', 'error')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell>
      <h2 className="font-headline-lg text-headline-lg text-on-surface mb-1">Sign in</h2>
      <p className="font-body-md text-body-md text-on-surface-variant mb-6">
        Enter your credentials to access the dispatch console.
      </p>

      <SuccessMessage>{searchParams.get('passwordChanged') === '1' ? 'Password updated. Sign in with your new password.' : null}</SuccessMessage>
      <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <ErrorMessage>{error}</ErrorMessage>

        <FormField
          label="Username"
          name="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          required
          autoComplete="username"
          placeholder="e.g. Kavindu"
        />
        <FormField
          label="Password"
          name="password"
          type={showPassword ? 'text' : 'password'}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
          autoComplete="current-password"
          placeholder="••••••••"
          rightSlot={
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              className="text-on-surface-variant hover:text-on-surface p-1"
              aria-label={showPassword ? 'Hide password' : 'Show password'}
            >
              <Icon name={showPassword ? 'eyeOff' : 'eye'} size={18} />
            </button>
          }
        />

        <Link to="/forgot-password" className="password-back-link" style={{ alignSelf: 'flex-end' }}>Forgot password?</Link>
        <Button type="submit" block loading={submitting}>
          {submitting ? 'Signing in…' : 'Log in'}
        </Button>
      </form>

      <div className="mt-6 text-center font-body-sm text-body-sm text-on-surface-variant">
        Dispatcher?{' '}
        <Link to="/register/dispatcher" className="text-primary font-semibold hover:underline">
          Create an account
        </Link>
      </div>
    </AuthShell>
  )
}