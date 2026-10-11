import { createContext, useContext, useState, useCallback, useMemo, useEffect } from 'react'
import * as authService from '../services/authService'

const AuthContext = createContext(null)

const TOKEN_KEY = 'smartfleet_token'
const USER_KEY = 'smartfleet_user'

function readStoredUser() {
  try {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

// Only these fields may ever land in AuthContext user state or localStorage.
// A password (current or new) must never be persisted here — this list is
// the enforcement point so no page has to remember that rule on its own.
const SAFE_USER_FIELDS = ['firstName', 'lastName', 'contactNumber', 'email', 'username', 'role', 'id']

export function AuthProvider({ children }) {
  const [user, setUser] = useState(readStoredUser)
  const [status, setStatus] = useState('idle') // idle | loading | error
  const [error, setError] = useState(null)

  const login = useCallback(async (username, password) => {
    setStatus('loading')
    setError(null)
    try {
      const data = await authService.login(username, password)
      localStorage.setItem(TOKEN_KEY, data.token)
      localStorage.setItem(USER_KEY, JSON.stringify(data.user))
      setUser(data.user)
      setStatus('idle')
      return data.user
    } catch (err) {
      setStatus('error')
      setError(err.message || 'Invalid username or password.')
      throw err
    }
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    setUser(null)
  }, [])

  useEffect(() => {
    const onStorage = event => { if (event.key === TOKEN_KEY && !event.newValue) logout() }
    window.addEventListener('smartfleet:session-ended', logout)
    window.addEventListener('storage', onStorage)
    return () => {
      window.removeEventListener('smartfleet:session-ended', logout)
      window.removeEventListener('storage', onStorage)
    }
  }, [logout])

  // Controlled profile update. Filters to SAFE_USER_FIELDS before touching
  // React state or localStorage, so a page can never accidentally persist
  // newPassword/confirmPassword into the user object — see Profile.jsx.
  const updateUser = useCallback((partial) => {
    setUser((prev) => {
      const safe = {}
      for (const key of SAFE_USER_FIELDS) {
        if (partial[key] !== undefined) safe[key] = partial[key]
      }
      const next = { ...prev, ...safe }
      localStorage.setItem(USER_KEY, JSON.stringify(next))
      return next
    })
  }, [])

  const value = useMemo(
    () => ({
      user,
      role: user?.role || null,
      isAuthenticated: !!user,
      status,
      error,
      login,
      logout,
      updateUser,
    }),
    [user, status, error, login, logout, updateUser]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
