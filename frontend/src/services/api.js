// Central HTTP client factory. Each microservice runs on its own port in
// dev (no API Gateway yet), so every service module gets a client pointed
// at its own backend — but they all share the same auth-header injection
// and error-normalization logic, defined once here.

import axios from 'axios'

export function createApiClient(baseURL) {
  const client = axios.create({
    baseURL,
    headers: {
      'Content-Type': 'application/json',
    },
  })

  // Attach the JWT to every outgoing request, if we have one.
  client.interceptors.request.use((config) => {
    const token = localStorage.getItem('smartfleet_token')

    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  })

  // Normalize errors into a shape the UI can render without knowing about axios.
  client.interceptors.response.use(
    (response) => response,
    (error) => {
      const status = error.response?.status
      const data = error.response?.data

      let message = 'Something went wrong. Please try again.'
      let fieldErrors = null

      if (status === 400 && data?.errors) {
        // ASP.NET model-validation shape:
        // { errors: { Field: [msg, ...] } }
        fieldErrors = data.errors
        message = 'Please fix the highlighted fields.'
      } else if (status === 401) {
        const isLogin = error.config?.url?.includes('/api/auth/login')
        if (!isLogin && error.config?.headers?.Authorization && error.config.headers.Authorization === `Bearer ${localStorage.getItem('smartfleet_token')}`) {
          localStorage.removeItem('smartfleet_token')
          localStorage.removeItem('smartfleet_user')
          window.dispatchEvent(new window.Event('smartfleet:session-ended'))
        }
        message = data?.message || (isLogin ? 'Invalid username or password.' : 'Your session has ended. Please sign in again.')
      } else if (status === 403) {
        message = 'You are not authorized to access this page.'
      } else if (status === 409) {
        // Conflict — e.g. duplicate username/email or registration number.
        message = data?.message || 'That value is already in use.'
      } else if (error.code === 'ERR_NETWORK') {
        message =
          'Cannot reach the server. Check your connection and try again.'
      } else if (data?.message) {
        message = data.message
      }

      return Promise.reject({
        status,
        message,
        fieldErrors,
        raw: error,
      })
    }
  )

  return client
}

// Default client — used by services that have not yet been split onto
// their own backend URL.
const defaultBaseURL =
  import.meta.env.VITE_API_BASE_URL ||
  import.meta.env.VITE_AUTH_API_URL ||
  'http://localhost:5100'

const api = createApiClient(defaultBaseURL)

export default api