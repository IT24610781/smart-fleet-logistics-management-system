import axios from 'axios'

const baseURL =
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'

const api = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
  },
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('smartfleet_token')

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const data = error.response?.data

    let message = 'Something went wrong. Please try again.'
    let fieldErrors = null

    if (status === 400 && data?.errors) {
      fieldErrors = data.errors
      message = 'Please fix the highlighted fields.'
    } else if (status === 401) {
      message = data?.message || 'Invalid username or password.'
    } else if (status === 403) {
      message = 'You are not authorized to access this page.'
    } else if (status === 409) {
      message =
        data?.message || 'That username or email is already in use.'
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

export default api