// Clearly isolated demo data (spec section 26). Nothing here is imported by
// any services/*.js file — only by pages, and only for modules whose real
// API isn't live yet. Swapping a page from mock to live data means changing
// the page's data source, not its markup.

export const MOCK_ADMIN_STATS = {
  totalDrivers: { value: 12, delta: { direction: 'up', text: '+2 this month' } },
  totalVehicles: { value: 24, delta: { direction: 'up', text: '+1 this month' } },
  availableVehicles: { value: 18, delta: { direction: 'up', text: '75% of fleet' } },
  activeTrips: { value: 7, delta: { direction: 'up', text: '+2% vs last week' } },
}

export const MOCK_DRIVERS = [
  { id: 'DRV-1001', name: 'Nadeesha Perera', username: 'nperera', email: 'nadeesha.p@smartfleet.lk', contact: '+94 71 234 5671', status: 'Active', createdDate: '2026-02-11' },
  { id: 'DRV-1002', name: 'Kasun Silva', username: 'ksilva', email: 'kasun.s@smartfleet.lk', contact: '+94 77 345 6782', status: 'Active', createdDate: '2026-02-19' },
  { id: 'DRV-1003', name: 'Ruwan Fernando', username: 'rfernando', email: 'ruwan.f@smartfleet.lk', contact: '+94 76 456 7893', status: 'Inactive', createdDate: '2026-03-02' },
  { id: 'DRV-1004', name: 'Ishara Jayasuriya', username: 'ijayasuriya', email: 'ishara.j@smartfleet.lk', contact: '+94 70 567 8904', status: 'Active', createdDate: '2026-03-14' },
  { id: 'DRV-1005', name: 'Tharindu Bandara', username: 'tbandara', email: 'tharindu.b@smartfleet.lk', contact: '+94 75 678 9015', status: 'Active', createdDate: '2026-04-05' },
]

export const DRIVER_OVERVIEW = {
  total: MOCK_DRIVERS.length,
  active: MOCK_DRIVERS.filter((d) => d.status === 'Active').length,
  inactive: MOCK_DRIVERS.filter((d) => d.status === 'Inactive').length,
}

// Fields mirror section 15's requested vehicle schema.
export const MOCK_VEHICLES = [
  { id: 'VEH-201', regNumber: 'WP CAB-4021', model: 'Isuzu ELF NPR', type: 'Truck (Medium)', capacity: '3.5 t', status: 'Available', currentDriver: '—', location: 'Colombo Depot', lastUpdate: '2 min ago' },
  { id: 'VEH-202', regNumber: 'WP CAA-1187', model: 'Toyota Dyna 300', type: 'Truck (Light)', capacity: '1.5 t', status: 'In Use', currentDriver: 'Kasun Silva', location: 'En route — Kandy', lastUpdate: '1 min ago' },
  { id: 'VEH-203', regNumber: 'WP KL-7723', model: 'Tata Ace', type: 'Mini Truck', capacity: '0.75 t', status: 'Available', currentDriver: '—', location: 'Colombo Depot', lastUpdate: '5 min ago' },
  { id: 'VEH-204', regNumber: 'WP CAD-9902', model: 'Mitsubishi Fuso Canter', type: 'Truck (Medium)', capacity: '3 t', status: 'Maintenance', currentDriver: '—', location: 'Service Center', lastUpdate: '3 hr ago' },
  { id: 'VEH-205', regNumber: 'WP CAB-5510', model: 'Isuzu Forward', type: 'Truck (Heavy)', capacity: '8 t', status: 'In Use', currentDriver: 'Nadeesha Perera', location: 'En route — Galle', lastUpdate: 'Just now' },
  { id: 'VEH-206', regNumber: 'WP CAA-3345', model: 'Nissan Atlas', type: 'Truck (Light)', capacity: '2 t', status: 'Inactive', currentDriver: '—', location: 'Unknown', lastUpdate: '2 days ago' },
]

export const FLEET_OVERVIEW = {
  available: MOCK_VEHICLES.filter((v) => v.status === 'Available').length,
  inUse: MOCK_VEHICLES.filter((v) => v.status === 'In Use').length,
  maintenance: MOCK_VEHICLES.filter((v) => v.status === 'Maintenance').length,
  inactive: MOCK_VEHICLES.filter((v) => v.status === 'Inactive').length,
}

export const MOCK_TRIPS = [
  { id: 'TRP-3001', origin: 'Colombo Depot', destination: 'Kandy Warehouse', vehicle: 'VEH-202', driver: 'Kasun Silva', status: 'In Progress', scheduledTime: '08:30 AM', createdDate: '2026-08-30' },
  { id: 'TRP-3002', origin: 'Colombo Port', destination: 'Galle Terminal', vehicle: 'VEH-205', driver: 'Nadeesha Perera', status: 'In Progress', scheduledTime: '09:15 AM', createdDate: '2026-08-31' },
  { id: 'TRP-3003', origin: 'Colombo Depot', destination: 'Negombo Hub', vehicle: '—', driver: '—', status: 'Pending', scheduledTime: '11:00 AM', createdDate: '2026-09-01' },
  { id: 'TRP-3004', origin: 'Kandy Warehouse', destination: 'Colombo Depot', vehicle: 'VEH-201', driver: 'Ishara Jayasuriya', status: 'Assigned', scheduledTime: '01:30 PM', createdDate: '2026-09-02' },
  { id: 'TRP-3005', origin: 'Colombo Depot', destination: 'Matara Yard', vehicle: 'VEH-203', driver: 'Tharindu Bandara', status: 'Completed', scheduledTime: '07:00 AM', createdDate: '2026-08-27' },
  { id: 'TRP-3006', origin: 'Colombo Depot', destination: 'Jaffna Hub', vehicle: '—', driver: 'Ruwan Fernando', status: 'Rejected', scheduledTime: '06:45 AM', createdDate: '2026-08-25' },
]

export const MOCK_DRIVER_TRIPS = [
  { id: 'TRP-3004', origin: 'Kandy Warehouse', destination: 'Colombo Depot', vehicle: 'VEH-201', date: '2026-09-03', scheduledTime: '01:30 PM', status: 'Assigned' },
  { id: 'TRP-2988', origin: 'Colombo Depot', destination: 'Kurunegala Hub', vehicle: 'VEH-201', date: '2026-08-29', scheduledTime: '09:00 AM', status: 'Completed' },
  { id: 'TRP-2971', origin: 'Colombo Depot', destination: 'Anuradhapura Yard', vehicle: 'VEH-203', date: '2026-08-24', scheduledTime: '10:15 AM', status: 'Completed' },
]

// Mixed-type system activity feed for the Admin/Dispatcher "Recent Activity"
// sections — distinct from the Recent Trips table.
export const RECENT_ACTIVITY = [
  { id: 'ACT-1', type: 'driver', text: 'Driver account created — Ishara Jayasuriya', time: '18 min ago' },
  { id: 'ACT-2', type: 'trip', text: 'Trip TRP-3005 completed', time: '1 hr ago' },
  { id: 'ACT-3', type: 'vehicle', text: 'VEH-202 assigned to Kasun Silva', time: '2 hr ago' },
  { id: 'ACT-4', type: 'maintenance', text: 'Maintenance recorded for VEH-204', time: '3 hr ago' },
  { id: 'ACT-5', type: 'trip', text: 'Trip TRP-3006 rejected by driver', time: '5 hr ago' },
]

export const MOCK_FLEET_UTILIZATION = {
  totalVehicles: 24, availableVehicles: 18, activeVehicles: 6, utilizationPercent: 25,
  trend: [18, 20, 19, 22, 21, 24, 25],
}

export const MOCK_TRIP_COMPLETION = {
  totalTrips: 132, completed: 104, pending: 14, rejected: 14, completionPercent: 79,
  trend: [70, 74, 73, 77, 76, 78, 79],
}

export const TOP_DRIVERS_BY_TRIPS = [
  { name: 'Nadeesha Perera', trips: 28 },
  { name: 'Kasun Silva', trips: 24 },
  { name: 'Tharindu Bandara', trips: 19 },
  { name: 'Ishara Jayasuriya', trips: 15 },
]
