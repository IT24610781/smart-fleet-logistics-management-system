import { createApiClient } from './api'

const trackingApi = createApiClient(
  import.meta.env.VITE_TRACKING_API_URL || 'http://localhost:5000'
)

export async function submitLocation({ tripId, latitude, longitude, observedAt }) {
  const { data } = await trackingApi.post('/api/tracking/locations', {
    tripId,
    latitude,
    longitude,
    observedAt,
  })
  return data
}

export async function getFleetLocations() {
  const { data } = await trackingApi.get('/api/tracking/fleet/latest')
  return data
}
