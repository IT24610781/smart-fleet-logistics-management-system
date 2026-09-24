// Auth & User Service - login (US-02) and Dispatcher self-registration (US-08).
import api from './api'

export async function login(username, password) {
  const { data } = await api.post('/api/auth/login', { username, password })
  return data // expected: { token, user: { id, username, role, ... } }
}

// US-08: public endpoint - no token needed. The server always assigns the
// "Dispatcher" role; the role is never sent from the browser.
export async function registerDispatcher(payload) {
  const { data } = await api.post('/api/auth/register/dispatcher', payload)
  return data
}
