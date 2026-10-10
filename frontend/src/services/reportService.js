import { createApiClient } from './api'
const trackingReportApi = createApiClient(
  import.meta.env.VITE_TRACKING_API_URL || 'http://localhost:5000'
)
const tripReportApi = createApiClient(
  import.meta.env.VITE_TRIP_API_URL || 'http://localhost:5294'
)

// params: { start, end } - Date objects or ISO strings.
export async function getFleetUtilizationReport(params) {
  const { data } = await trackingReportApi.get('/api/reports/fleet-utilization', {
    params: {
      start: toIso(params?.start),
      end: toIso(params?.end),
    },
  })
  return data
}

// SCRUM-143 (US-18): live. params: { start, end } - Date objects or ISO strings.
export async function getTripCompletionReport(params) {
  const { data } = await tripReportApi.get('/api/reports/trip-completion', {
    params: {
      start: toIso(params?.start),
      end: toIso(params?.end),
    },
  })
  return data
}

function toIso(value) {
  if (!value) return undefined
  return value instanceof Date ? value.toISOString() : value
}