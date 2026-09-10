// Reporting Service — not yet implemented by the backend.
import api from './api'

export async function getFleetUtilizationReport(params) {
  const { data } = await api.get('/api/reports/fleet-utilization', { params })
  return data
}

export async function getTripCompletionReport(params) {
  const { data } = await api.get('/api/reports/trip-completion', { params })
  return data
}
