// Fleet / Vehicle Service — not yet implemented by the backend.
// Kept as a placeholder so Vehicles.jsx can swap mock data for real calls
// later without touching any component code.
import api from './api'

export async function listVehicles() {
  const { data } = await api.get('/api/vehicles')
  return data
}

export async function updateVehicleStatus(vehicleId, status) {
  const { data } = await api.patch(`/api/vehicles/${vehicleId}/status`, { status })
  return data
}
