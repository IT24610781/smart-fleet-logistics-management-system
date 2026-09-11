// Fleet / Vehicle Service.
// Own client pointed at FleetVehicleService's own port — separate from the
// default api.js client (which points at AuthUserService), since there's
// no API Gateway yet and each service runs on a different port in dev.
import { createApiClient } from './api'

const vehicleApi = createApiClient(
  import.meta.env.VITE_VEHICLE_API_URL || 'http://localhost:5041'
)

// createVehicle is wired to the real POST /api/vehicles endpoint.
// listVehicles/updateVehicleStatus are kept as placeholders until
// their backend endpoints exist — Vehicles.jsx still reads mock data.
export async function createVehicle(form) {
  const { data } = await vehicleApi.post('/api/vehicles', {
    registrationNo: form.registrationNo,
    type: form.type,
    capacity: form.capacity,
  })
  return data
}

export async function listVehicles() {
  const { data } = await vehicleApi.get('/api/vehicles')
  return data
}

export async function updateVehicleStatus(vehicleId, status) {
  const { data } = await vehicleApi.patch(`/api/vehicles/${vehicleId}/status`, { status })
  return data
}