# Driver deactivation

## Account and trip rules

Admin can deactivate a Driver from the Drivers table after confirming the action.
This sets the existing identity account's is_active flag to false and updates its
timestamp. Account records, credentials and historical trips are preserved.
There is no automatic cancellation, reassignment or account deletion.

Deactivation is blocked when TripDispatchService reports an Assigned or
In-Progress trip for that Driver. Completed, Cancelled and unassigned Requested
trips do not block it. If Trip service cannot be reached, times out, rejects the
request or returns invalid data, the account stays active and the API returns 503.
Repeated deactivation of an inactive Driver returns success without changing history.

These are conservative defaults for this feature. They should be reflected in
the story acceptance criteria if the team adopts them.

## API

POST /api/users/drivers/{driverId}/deactivate, with an Admin Bearer token:
- 204: inactive, including an already inactive Driver.
- 401: missing/invalid/expired authentication.
- 403: authenticated non-Admin caller.
- 404: no such Driver, including an account with another role.
- 409: assigned or active trip prevents deactivation.
- 503: trip eligibility could not be verified.

Auth prepares deactivation through Admin-only
POST /api/trips/drivers/{driverId}/deactivation-check. A 204 confirms that Trip
has persisted its assignment block; a 409 means an assigned/active trip prevents
deactivation. This is an internal workflow operation, not a UI account toggle.

Inactive Drivers cannot log in. Auth, Trip and Fleet recheck account state after
JWT signature/lifetime validation on every authenticated Driver request. Old
Driver tokens are rejected with 401 on subsequent requests. Requests already in
flight are not forcibly cancelled. Browser state is cleared when the current
token receives 401, including other tabs through the storage event. An idle page
is not a live notification channel; it signs out when it next calls an API.
Temporary status-check outages return 503 rather than signing the Driver out.

Trip assignment verifies that the selected Driver exists, has the Driver role
and is active before changing vehicle or trip state. The assignment picker shows
active Drivers only and retains inactive Driver names for historical trip details.

## Configuration and running services

For local dotnet launch profiles:
- Auth: TripService__BaseUrl=http://localhost:5294/
- Trip and Fleet: AuthService__BaseUrl=http://localhost:5100/

Those URLs are Development defaults. If Trip is run in Docker on host port 5051
while Auth runs locally, set TripService__BaseUrl=http://localhost:5051/.
Production must configure these URLs explicitly. Base URLs should use a trailing
slash and must point to the service root, without /api.

docker-compose.yml sets the internal Auth/Trip addresses. Rebuild/restart only
the service containers; database volumes are preserved. For an existing Trip database, first apply the
safe additive migration database/trip-dispatch/driver-deactivation.sql. Fresh
databases get this table from schema.sql. No account-table migration is needed:

    docker compose up -d --build --no-deps auth-service trip-service fleet-service

All three services must use the same JWT issuer, audience and signing key.
Auth forwards the Admin token to Trip for the eligibility lookup. Trip forwards
the Dispatcher token to Auth for assignment validation. Trip/Fleet forward
Driver tokens to Auth for session status checks. Lookups have a five-second timeout.

## Verification

    dotnet test tests/AuthUserService.Tests --filter FullyQualifiedName~DriverDeactivationTests
    dotnet test tests/TripDispatchService.Tests --filter "FullyQualifiedName~DriverAccountAuthorizationTests|FullyQualifiedName~DriverEligibilityTests"
    dotnet test tests/FleetVehicleService.Tests --filter FullyQualifiedName~DriverAccountAuthorizationTests
    npm.cmd run build --prefix frontend
    npm.cmd run lint --prefix frontend

Set TEST_TRIP_GUARD_CONNECTION to a MySQL test server with create/drop-database
permissions to run DriverAssignmentGuardDatabaseTests. Those tests create GUID-named
temporary databases, verify the SQL lock and trip-state checks, and clean up only
their own databases. CI sets this connection to its dedicated MySQL test service.

The isolated tests use fake account storage and controlled HTTP responses; they
do not modify production/user records. Auth API tests execute the real controller,
JWT authentication and authorization pipeline. DriverDeactivationDatabaseTests
also verifies the real Auth repository persists the inactive flag without changing
the account ID, creation timestamp or password hash. Run it with the normal Auth
test database configuration, never against a demo/production database.
Existing Trip workflow/lifecycle
unit tests are also included in verification.

## Concurrent operations and recovery

Trip serializes assignment and deactivation checks for each Driver using a
MySQL advisory lock. Assignment holds that lock across account validation and
the existing assignment operation. Deactivation acquires the same lock, checks
for Assigned/In-Progress trips, and persists an assignment block before Auth
marks the account inactive. The block survives service restarts and applies
across Trip instances using the same MySQL server.

If assignment wins the lock, deactivation sees its trip and returns 409.
If deactivation wins, new assignments are blocked even while Auth completes its
account update. Completed and Cancelled history is preserved.

If Auth's database write fails after Trip has prepared deactivation, new
assignments remain blocked while the account may still be active. Retry the Admin
deactivation action once Auth is healthy; Trip preparation is idempotent. This
intentional pending state prevents an interrupted operation from allowing a new
assignment. Reactivation is outside this feature and would require an explicit
workflow that also clears the Trip assignment block.

Existing in-flight requests are not forcibly cancelled. This coordination does
not change the existing Fleet-to-Trip compensation behavior for vehicle updates.
