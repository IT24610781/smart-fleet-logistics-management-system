// Auth & User Service — currently implements only what the backend exposes
// today (login). Registration is scaffolded for a later sprint per the SRS.
import api from './api'

export async function login(username, password) {
  const { data } = await api.post('/api/auth/login', { username, password })
  return data // expected: { token, user: { id, username, role, ... } }
}

// NOT YET IMPLEMENTED BY THE BACKEND.
// Wired to the service layer now so the DispatcherRegister page can call a
// real endpoint the moment it exists, without any UI changes.
export async function registerDispatcher(payload) {
  const { data } = await api.post('/api/auth/register/dispatcher', payload)
  return data
}
