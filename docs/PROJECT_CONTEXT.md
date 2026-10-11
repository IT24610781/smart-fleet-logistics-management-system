# Smart Fleet project context

Reviewed from repository source on 2026-09-20. This is a navigation and implementation-status guide for future work, not a runtime verification or exhaustive code audit. Recheck source when making changes; this snapshot can become stale. No credentials are recorded here.

## Purpose and stack

Fleet and logistics management for Admin, Dispatcher, and Driver roles. React 18, React Router 6, Axios, Vite 5, and Tailwind CSS 3 frontend; four ASP.NET Core services targeting .NET 10. `global.json` pins SDK 10.0.400. Implemented persistence uses MySqlConnector and parameterized ADO.NET SQL, not Entity Framework. Kafka supports vehicle status events.

## Repository map

- `frontend/src/App.jsx`: public, role-protected, and shared routes.
- `frontend/src/main.jsx`: BrowserRouter, AuthProvider, ToastProvider, global styles.
- `frontend/src/pages/`: admin, dispatcher, driver, auth, and shared screens.
- `frontend/src/components/`: shared layout, controls, tables, dialogs, and form fields.
- `frontend/src/services/`: API clients and endpoint wrappers.
- `frontend/src/utils/mockData.js`: demonstration dashboard, trip, tracking, and report data.
- `services/AuthUserService/`: login, driver creation/listing, own profile, database health.
- `services/FleetVehicleService/`: vehicle CRUD and status events.
- `services/TripDispatchService/` and `services/TrackingTelemetryService/`: starter weather-forecast APIs; domain APIs are not implemented.
- `database/auth-user/schema.sql` and `database/fleet-vehicle/schema.sql`: database definitions.
- `tests/`: xUnit validation/integration tests, Selenium browser tests, and a JMeter plan.
- `.github/workflows/build.yml`: build, test, Docker, and Azure deployment workflow.
- `SmartFleet.slnx`: .NET solution; `docker-compose.yml`: local infrastructure and Fleet service.

## Implementation status

| Area | Source implementation |
| --- | --- |
| Login | Real API, BCrypt password verification, JWT issuance, role-based redirect |
| Logout | Clears local browser token/user state; no server revocation flow |
| Drivers | Admin can create and list real accounts; list filters by name/username and active status |
| Driver row actions | View, Edit, and Deactivate buttons have no handlers |
| Profile | Authenticated users can read/update their own profile and optionally change password |
| Vehicles | Real create/list/update/delete APIs and frontend integration |
| Vehicle deletion eligibility | Interface exists, but registered stub always reports no active/upcoming trips |
| Vehicle status events | Kafka publisher sends to `vehicle.status.events` when status changes |
| Dispatcher registration | Form and API wrapper exist; backend endpoint absent |
| Dashboards, reports, tracking | Mock/demo data rather than complete live integration |
| Trips | Mock listings and wrappers/actions targeting unimplemented endpoints |

## Authentication and frontend integration

`Login.jsx` validates nonempty credentials, trims username, invokes AuthContext login, shows toast/error feedback, and redirects to the role dashboard. It also supports password visibility and links to dispatcher registration.

`AuthContext.jsx` stores `smartfleet_token` and `smartfleet_user` in localStorage. Authentication state is derived from the stored user; the frontend does not independently enforce JWT expiry. `updateUser` filters incoming profile updates to permitted user fields. `ProtectedRoute.jsx` redirects unauthenticated users to `/login` and wrong-role users to `/403`; backend authorization is separate.

`services/api.js` creates Axios clients, adds bearer tokens, and normalizes API/network errors. Default API URL precedence: `VITE_API_BASE_URL`, then `VITE_AUTH_API_URL`, then `http://localhost:5100`. Vehicles use a separate client: `VITE_VEHICLE_API_URL`, then `http://localhost:5041`. Trip/tracking/report wrappers currently use the default client and will require proper service routing when implemented.

Routes are grouped under `/admin`, `/dispatcher`, and `/driver`. `/profile` is available to all three authenticated roles. Navigation is defined in `components/layout/navConfig.js`.

## Implemented API contracts

| Method and route | Access / purpose |
| --- | --- |
| `POST /api/auth/login` | Public; returns token, expiry, and user |
| `GET /api/health/database` | Database connectivity probe |
| `POST /api/users/drivers` | Admin; creates a Driver account |
| `GET /api/users/drivers` | Admin; lists drivers |
| `GET /api/users/me` | Authenticated user's profile |
| `PUT /api/users/me` | Authenticated user's profile update |
| `POST /api/vehicles` | Admin; creates vehicle |
| `GET /api/vehicles` | Admin or Dispatcher; lists vehicles |
| `PUT /api/vehicles/{id}` | Admin; updates vehicle |
| `DELETE /api/vehicles/{id}` | Admin; deletes vehicle |

Both implemented services follow Controller -> Service -> Repository -> MySQL. Auth startup runs AdminSeeder. Both validate JWT issuer, audience, signing key, and lifetime.

## Data and business rules

- `fleet_identity_db.users`: GUID identifier, unique username/email, BCrypt password hash, first/last name, contact number, role, active flag, timestamps. Roles: `Admin`, `Dispatcher`, `Driver`.
- Driver creation sets role to Driver, requires profile fields, checks duplicate username/email, and enforces a minimum eight-character password.
- Profile updates change first name, last name, contact number, and optionally password. Identity comes from JWT claims.
- `fleet_vehicle_db.vehicles`: GUID identifier, unique registration number, type, capacity, status, timestamps. Capacity is stored as text but service validation requires a positive integer.
- Exact vehicle statuses: `Available`, `On Trip`, `Under Maintenance`. New vehicles start Available.
- Status updates save the database change before awaiting Kafka publishing. A publish failure can therefore occur after persistence; there is no outbox in this path.

## UI conventions and known integration details

Reuse `AppShell`, Sidebar/Topbar, `FormField`, `Button`, `DataTable`, `StatusBadge`, error components, confirmation dialogs, and ToastContext. Tailwind theme values live in `frontend/tailwind.config.js`; `src/index.css` imports fonts and `styles/legacy.css`. Some pages use the legacy CSS classes while newer screens use Tailwind tokens.

`Drivers.jsx` fetches real users on mount and maps them to table rows. `EditVehicle.jsx` expects a vehicle supplied through navigation state; opening the route without that state displays a return-to-list prompt rather than fetching by ID.

Comments in `services/userService.js` incorrectly label profile endpoints as unimplemented. Prefer actual controllers over stale comments or broad README feature claims. The README describes Kafka as planned, but Fleet already contains a concrete publisher.

## Development and verification map

- Frontend scripts, from `frontend/`: `npm ci`, `npm run dev`, `npm run lint`, `npm run build`.
- .NET commands from root: `dotnet restore SmartFleet.slnx`, `dotnet build SmartFleet.slnx`; run individual services with `dotnet run --project services/<ServiceName>`.
- Default frontend Vite origin: localhost:5173. Auth HTTP profile: localhost:5100; Fleet HTTP profile: localhost:5041. Check launch settings and environment overrides before running.
- Compose defines Auth MySQL at host port 3607, Fleet MySQL at 3617, Fleet API at 5041, and Kafka with exposed ports 9092/9094. It does not define the complete application stack.
- Runtime setup requires database schemas, connection strings, JWT settings, and Kafka configuration for the Fleet publisher. Consult local configuration without copying secrets into documentation.
- Auth/Fleet tests include validation and API integration scenarios. Database integration tests require configured MySQL test databases; CI creates `fleet_identity_db_test` and `fleet_vehicle_db_test`.
- Selenium tests cover login/logout/profile, driver creation, and vehicle CRUD. These need the browser and application services configured separately.
- Trip and Tracking test projects currently contain empty starter tests.
- JMeter plan: `tests/performance/FleetVehiclePerformanceTest.jmx`.
- CI builds/tests .NET, lints/builds frontend with Node 24, and builds Auth/Fleet Docker images. Auth Azure deployment is configured for pushes to dev/main; Fleet container deployment for dev. Configuration does not establish that deployment currently succeeds.

## Review scope

This pass inspected structure, routing, authentication, client wrappers, principal controllers/services, database schemas, active Login/Drivers pages, page integration references, UI conventions, and test/CI configuration. The application and tests were not executed. No application behavior was changed. This guide preserves useful context across sessions, but future assistants must read it; it is not a guarantee of automatic or permanent conversational memory.
