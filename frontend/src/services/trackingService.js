// Tracking / Telemetry Service — not yet implemented by the backend.
import api from './api'

export async function getFleetLocations() {
  const { data } = await api.get('/api/tracking/vehicles')
  return data
}
