// Fleet / Vehicle Service.
import { createApiClient } from './api'

const vehicleApi = createApiClient(
  import.meta.env.VITE_VEHICLE_API_URL || 'http://localhost:5041'
)

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

// US-06: full update of a vehicle's editable fields.
export async function updateVehicle(id, form) {
  const { data } = await vehicleApi.put(`/api/vehicles/${id}`, {
    type: form.type,
    capacity: form.capacity,
    status: form.status,
  })
  return data
}