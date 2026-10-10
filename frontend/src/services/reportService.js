import { createApiClient } from './api'
const reportApi = createApiClient(
  import.meta.env.VITE_TRACKING_API_URL || 'http://localhost:5000'
)

// params: { start, end } - Date objects or ISO strings.
export async function getFleetUtilizationReport(params) {
  const { data } = await reportApi.get('/api/reports/fleet-utilization', {
    params: {
      start: toIso(params?.start),
      end: toIso(params?.end),
    },
  })
  return data
}

export async function getTripCompletionReport(params) {
  const { data } = await reportApi.get('/api/reports/trip-completion', { params })
  return data
}

function toIso(value) {
  if (!value) return undefined
  return value instanceof Date ? value.toISOString() : value
}