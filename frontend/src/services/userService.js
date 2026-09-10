// User Service — driver account creation (US-01) and profile management.
import api from './api'

export async function createDriver(payload) {
  const { data } = await api.post('/api/users/drivers', payload)
  return data
}

// NOT YET IMPLEMENTED BY THE BACKEND — endpoint name is a placeholder.
export async function listDrivers() {
  const { data } = await api.get('/api/users/drivers')
  return data
}

// NOT YET IMPLEMENTED BY THE BACKEND — endpoint name is a placeholder.
export async function getProfile() {
  const { data } = await api.get('/api/users/me')
  return data
}

// NOT YET IMPLEMENTED BY THE BACKEND — endpoint name is a placeholder.
export async function updateProfile(payload) {
  const { data } = await api.put('/api/users/me', payload)
  return data
}
