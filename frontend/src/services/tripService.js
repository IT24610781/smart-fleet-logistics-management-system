import { createApiClient } from './api'

const tripApi = createApiClient(
  import.meta.env.VITE_TRIP_API_URL || 'http://localhost:5294'
)

//US-09 create a trip request. pickupLat/Lng and dropoffLat/Lng are optional
export async function createTrip(payload) {
  const { data } = await tripApi.post('/api/trips', {
    pickupLocation: payload.pickupLocation,
    pickupLat: payload.pickupLat,
    pickupLng: payload.pickupLng,
    dropoffLocation: payload.dropoffLocation,
    dropoffLat: payload.dropoffLat,
    dropoffLng: payload.dropoffLng,
    requiredCapacity: payload.requiredCapacity,
  })
  return data
}

export async function listTrips(status) {
  const { data } = await tripApi.get('/api/trips', {
    params: status ? { status } : undefined,
  })
  return data
}

export async function respondToTrip(tripId, decision) {
  const { data } = await tripApi.post(`/api/trips/${tripId}/respond`, { decision })
  return data
}

export async function updateTripStatus(tripId, status) {
  const { data } = await tripApi.patch(`/api/trips/${tripId}/status`, { status })
  return data
}

// Trip Details page: fetch one trip's full information.
export async function getTrip(tripId) {
  const { data } = await tripApi.get(`/api/trips/${tripId}`)
  return data
}

//US-10 ranked list of vehicles that fit a trip's required capacity.
export async function getSuggestions(tripId) {
  const { data } = await tripApi.get(`/api/trips/${tripId}/suggestions`)
  return data
}

export async function previewSuggestions(capacity) {
  const { data } = await tripApi.get('/api/trips/preview-suggestions', { params: { capacity } })
  return data
}

//US-11/US-12 assign a suggested vehicle AND a driver to a trip.
export async function assignVehicle(tripId, vehicleId, driverId) {
  const { data } = await tripApi.post(`/api/trips/${tripId}/assign`, { vehicleId, driverId })
  return data
}

//US-12 the current driver's own trips.
export async function getMyTrips() {
  const { data } = await tripApi.get('/api/trips/mine')
  return data
}

// SCRUM-148/149 (US-19): Driver Performance Report. Lives on the Trip
// Dispatch API (not reportService.js's Tracking API base URL) because the
export async function getDriverPerformanceReport(params) {
  const { data } = await tripApi.get('/api/reports/driver-performance', {
    params: {
      start: params?.start instanceof Date ? params.start.toISOString() : params?.start,
      end: params?.end instanceof Date ? params.end.toISOString() : params?.end,
    },
  })
  return data
}