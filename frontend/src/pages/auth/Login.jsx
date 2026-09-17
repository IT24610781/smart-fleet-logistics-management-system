import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useToast } from '../../context/ToastContext'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import { ErrorMessage } from '../../components/common/ErrorMessage'

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
    <div className="min-h-screen flex items-center justify-center bg-surface p-4 sm:p-8">
      <div className="w-full max-w-5xl grid grid-cols-1 lg:grid-cols-12 rounded-xl shadow-xl overflow-hidden bg-surface-container-lowest">
        {/* Left: brand panel */}
        <div className="lg:col-span-6 bg-primary p-8 sm:p-12 flex flex-col justify-between relative overflow-hidden text-on-primary">
          <div className="absolute -right-24 -top-24 w-96 h-96 rounded-full bg-secondary/15 blur-3xl pointer-events-none" />
          <div className="absolute -left-20 -bottom-20 w-80 h-80 rounded-full bg-tertiary-container/30 blur-2xl pointer-events-none" />

          <div className="relative z-10 space-y-8">
            <div className="flex items-center gap-4">
              <img src="/fleet-mark.svg" alt="" className="w-12 h-12 rounded-lg shadow-sm" />
              <div>
                <span className="font-headline-sm text-headline-sm tracking-tight text-surface-container-lowest">
                  Smart Fleet
                </span>
                <p className="font-label-md text-label-md text-primary-fixed-dim uppercase tracking-wider">
                  Fleet Logistics &amp; Operations
                </p>
              </div>
            </div>

            <div className="space-y-3 max-w-md">
              <h1 className="font-headline-xl-mobile lg:font-headline-xl text-headline-xl-mobile lg:text-headline-xl font-bold tracking-tight text-surface-container-lowest">
                Real-time fleet telematics &amp; dynamic dispatch.
              </h1>
              <p className="font-body-lg text-body-lg text-primary-fixed-dim leading-relaxed">
                A unified platform for driver routing, mission manifests, and dispatch state.
              </p>
            </div>

            <div className="space-y-3 pt-2 hidden sm:block">
              {[
                { icon: 'route', title: 'Plan every route', desc: 'Create missions, assign vehicles, and keep schedules moving.' },
                { icon: 'visibility', title: 'See the whole fleet', desc: 'Track vehicle availability and trip progress from one place.' },
              ].map((f) => (
                <div key={f.title} className="flex items-center gap-3 p-3 rounded-lg bg-white/5">
                  <div className="w-8 h-8 rounded bg-secondary/20 flex items-center justify-center text-secondary-fixed">
                    <span className="material-symbols-outlined text-lg">{f.icon}</span>
                  </div>
                  <div>
                    <p className="font-title-md text-title-md text-surface-container-lowest">{f.title}</p>
                    <p className="font-body-sm text-body-sm text-primary-fixed-dim">{f.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="relative z-10 pt-8 mt-4 hidden sm:flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-secondary-fixed" />
              <span className="font-body-sm text-body-sm text-primary-fixed">One fleet. One operational view.</span>
            </div>
          </div>
        </div>

        {/* Right: auth form */}
        <div className="lg:col-span-6 p-8 sm:p-12 flex flex-col justify-center">
          <h2 className="font-headline-lg text-headline-lg text-on-surface mb-1">Sign in</h2>
          <p className="font-body-md text-body-md text-on-surface-variant mb-6">
            Enter your credentials to access the dispatch console.
          </p>

          <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
            <ErrorMessage>{error}</ErrorMessage>

            <FormField
              label="Username"
              name="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              autoComplete="username"
              placeholder="e.g. driver01"
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
        </div>
      </div>
    </div>
  )
}
