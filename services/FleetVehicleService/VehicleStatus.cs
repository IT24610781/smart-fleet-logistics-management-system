@FleetVehicleService_HostAddress = http://localhost:5041

### Happy path
POST {{FleetVehicleService_HostAddress}}/api/vehicles
Content-Type: application/json

{
  "RegistrationNo": "WP-CAB-1234",
  "Type": "Truck",
  "Capacity": "5000"
}

### Non-numeric capacity (AC Scenario 3)
POST {{FleetVehicleService_HostAddress}}/api/vehicles
Content-Type: application/json

{
  "RegistrationNo": "WP-CAB-5678",
  "Type": "Van",
  "Capacity": "abc"
}

### Duplicate registration (AC Scenario 2) - run AFTER the happy path succeeds
POST {{FleetVehicleService_HostAddress}}/api/vehicles
Content-Type: application/json

{
  "RegistrationNo": "WP-CAB-1234",
  "Type": "Van",
  "Capacity": "3000"
}

### Zero/negative capacity
POST {{FleetVehicleService_HostAddress}}/api/vehicles
Content-Type: application/json

{
  "RegistrationNo": "WP-CAB-9999",
  "Type": "Truck",
  "Capacity": "0"
}