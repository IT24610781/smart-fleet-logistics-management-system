import { useState } from 'react'
import { useNavigate, useLocation, Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import FormField from '../../components/forms/FormField'
import Button from '../../components/common/Button'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import './AuthLayout.css'

const HOME_BY_ROLE = {
  Admin: '/admin/dashboard',
  Dispatcher: '/dispatcher/dashboard',
  Driver: '/driver/dashboard',
}

// Priority 1 / US-02 — connects to POST /api/auth/login.
export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
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
      const redirectTo = location.state?.from?.pathname || HOME_BY_ROLE[user.role] || '/login'
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setError(err.message || 'Invalid username or password.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <div className="auth-brand">
          <img src="/fleet-mark.svg" alt="" />
          <h1>Smart Fleet</h1>
          <p>Fleet &amp; logistics management</p>
        </div>

        <form className="auth-form" onSubmit={handleSubmit} noValidate>
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
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoComplete="current-password"
            placeholder="••••••••"
          />

          <Button type="submit" block loading={submitting}>
            {submitting ? 'Signing in…' : 'Log in'}
          </Button>
        </form>

        <div className="auth-foot">
          Dispatcher? <Link to="/register/dispatcher">Create an account</Link>
        </div>
      </div>
    </div>
  )
}
