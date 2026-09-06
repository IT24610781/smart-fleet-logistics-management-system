// Dispatch / Trip Service — not yet implemented by the backend.
import api from './api'

export async function listTrips() {
  const { data } = await api.get('/api/trips')
  return data
}

export async function createTripRequest(payload) {
  const { data } = await api.post('/api/trips', payload)
  return data
}

export async function respondToTrip(tripId, decision) {
  const { data } = await api.post(`/api/trips/${tripId}/respond`, { decision })
  return data
}

export async function updateTripStatus(tripId, status) {
  const { data } = await api.patch(`/api/trips/${tripId}/status`, { status })
  return data
}
