import { Routes, Route, Navigate } from 'react-router-dom'
import { useAuth } from './context/AuthContext'
import ProtectedRoute from './routes/ProtectedRoute'

import Login from './pages/auth/Login'
import DispatcherRegister from './pages/auth/DispatcherRegister'

import AdminDashboard from './pages/admin/AdminDashboard'
import Drivers from './pages/admin/Drivers'
import CreateDriver from './pages/admin/CreateDriver'
import Vehicles from './pages/admin/Vehicles'

import DispatcherDashboard from './pages/dispatcher/DispatcherDashboard'
import Trips from './pages/dispatcher/Trips'
import Tracking from './pages/dispatcher/Tracking'

import DriverDashboard from './pages/driver/DriverDashboard'
import MyTrips from './pages/driver/MyTrips'

import Reports from './pages/shared/Reports'
import Profile from './pages/shared/Profile'

const HOME_BY_ROLE = {
  Admin: '/admin/dashboard',
  Dispatcher: '/dispatcher/dashboard',
  Driver: '/driver/dashboard',
}

function RootRedirect() {
  const { isAuthenticated, role } = useAuth()
  if (isAuthenticated) return <Navigate to={HOME_BY_ROLE[role] || '/login'} replace />
  return <Navigate to="/login" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />

      {/* Public */}
      <Route path="/login" element={<Login />} />
      <Route path="/register/dispatcher" element={<DispatcherRegister />} />

      {/* Admin */}
      <Route path="/admin/dashboard" element={<ProtectedRoute allowedRoles={['Admin']}><AdminDashboard /></ProtectedRoute>} />
      <Route path="/admin/drivers" element={<ProtectedRoute allowedRoles={['Admin']}><Drivers /></ProtectedRoute>} />
      <Route path="/admin/drivers/create" element={<ProtectedRoute allowedRoles={['Admin']}><CreateDriver /></ProtectedRoute>} />
      <Route path="/admin/vehicles" element={<ProtectedRoute allowedRoles={['Admin']}><Vehicles /></ProtectedRoute>} />
      <Route path="/admin/reports" element={<ProtectedRoute allowedRoles={['Admin']}><Reports /></ProtectedRoute>} />

      {/* Dispatcher */}
      <Route path="/dispatcher/dashboard" element={<ProtectedRoute allowedRoles={['Dispatcher']}><DispatcherDashboard /></ProtectedRoute>} />
      <Route path="/dispatcher/trips" element={<ProtectedRoute allowedRoles={['Dispatcher']}><Trips /></ProtectedRoute>} />
      <Route path="/dispatcher/tracking" element={<ProtectedRoute allowedRoles={['Dispatcher']}><Tracking /></ProtectedRoute>} />
      <Route path="/dispatcher/reports" element={<ProtectedRoute allowedRoles={['Dispatcher']}><Reports /></ProtectedRoute>} />

      {/* Driver */}
      <Route path="/driver/dashboard" element={<ProtectedRoute allowedRoles={['Driver']}><DriverDashboard /></ProtectedRoute>} />
      <Route path="/driver/trips" element={<ProtectedRoute allowedRoles={['Driver']}><MyTrips /></ProtectedRoute>} />

      {/* Shared across all authenticated roles */}
      <Route path="/profile" element={<ProtectedRoute allowedRoles={['Admin', 'Dispatcher', 'Driver']}><Profile /></ProtectedRoute>} />

      <Route path="*" element={<RootRedirect />} />
    </Routes>
  )
}
