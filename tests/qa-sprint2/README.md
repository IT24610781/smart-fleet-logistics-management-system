# Sprint 2 QA automation

Scope: the FleetVehicleService vehicle-management stories - vehicle create,
list, update and delete - plus the capacity-field parsing rule and the
Driver-account-status check that gates Fleet access. This is a catalogue of
the existing FleetVehicleService coverage, in the same format as the Sprint 3
and Sprint 4 QA READMEs; it does not add new test files.

None of the files in `FleetVehicleService.Tests` carry an explicit US number
in their headers (unlike several AuthUserService and TrackingTelemetryService
files) — confirm the exact story IDs with the team if you need to cite this
table against specific requirements. `SCRUM-85` and `SCRUM-86` are referenced
inline in two files as the originating tickets for specific bugs, not stories.

## Included code

| File | Coverage |
| --- | --- |
| `../FleetVehicleService.Tests/VehicleCreateEndpointTests.cs` | Eleven endpoint checks: creating a vehicle, including SCRUM-86's oversized-registration-number case |
| `../FleetVehicleService.Tests/VehicleListEndpointTests.cs` | Five endpoint checks: listing/filtering vehicles |
| `../FleetVehicleService.Tests/VehicleUpdateEndpointTests.cs` | Ten endpoint checks, including the same SCRUM-86 class of bug on update |
| `../FleetVehicleService.Tests/VehicleDeleteEndpointTests.cs` | Seven endpoint checks, including SCRUM-85: a vehicle "On Trip" must not be deletable |
| `../FleetVehicleService.Tests/CapacityParserTests.cs` | Three pure unit checks (no DB/server) for parsing `"12"`, `"500 kg"`, `"8 persons"`-style capacity input |
| `../FleetVehicleService.Tests/DriverAccountAuthorizationTests.cs` | Six checks on the Fleet-side gate that calls Auth to confirm a Driver's account is active before allowing an action |
| `../Frontend.SeleniumTests/VehicleCreatePageTests.cs` | Six live browser scenarios |
| `../Frontend.SeleniumTests/VehicleListPageTests.cs` | Three live browser scenarios |
| `../Frontend.SeleniumTests/VehicleUpdatePageTests.cs` | Four live browser scenarios |
| `../Frontend.SeleniumTests/VehicleDeletePageTests.cs` | Two live browser scenarios |
| `../performance/FleetVehiclePerformanceTest.jmx` | SCRUM-79: vehicle create/list/update load test, NFR target under 2s per request |

`FleetVehicleService.Tests/UnitTest1.cs` is the unused default xUnit project
scaffold (`Test1()` with an empty body) — it is not real coverage and is a
candidate for deletion, not something to report against a story.

Run the commands below in PowerShell from the repository root. .NET 10 is required.

## 1 Run the unit/integration checks

`CapacityParserTests.cs` needs nothing — it is pure logic. Everything else in
`FleetVehicleService.Tests` boots the real app in-memory against a real MySQL
schema, so a reachable test database is required first.

`CustomWebApplicationFactory.cs` defaults to:

```
Server=127.0.0.1;Port=3317;Database=fleet_vehicle_db_test;Uid=root;Pwd=rootpassword;
```

That port and password already match the `docker-compose` `fleet-db`
container (host port `3317`, password `rootpassword`), so only the database
itself needs creating — the default connection string works as-is once it
exists:

```powershell
# One-time: create the test schema against the existing fleet-db container
Get-Content database/fleet-vehicle/schema.sql | docker exec -i smart-fleet-logistics-management-system-fleet-db-1 mysql -uroot -prootpassword -e "CREATE DATABASE IF NOT EXISTS fleet_vehicle_db_test;"
Get-Content database/fleet-vehicle/schema.sql | docker exec -i smart-fleet-logistics-management-system-fleet-db-1 mysql -uroot -prootpassword fleet_vehicle_db_test

dotnet test tests/FleetVehicleService.Tests/FleetVehicleService.Tests.csproj --filter "FullyQualifiedName~VehicleCreateEndpointTests|FullyQualifiedName~VehicleListEndpointTests|FullyQualifiedName~VehicleUpdateEndpointTests|FullyQualifiedName~VehicleDeleteEndpointTests|FullyQualifiedName~CapacityParserTests|FullyQualifiedName~DriverAccountAuthorizationTests" --logger "trx;LogFileName=Sprint02-unit.trx" --results-directory "TestResults/QA-Sprint02/unit"
```

Tests insert and read real rows in whatever database the connection string
points to — never run this against a database you use for manual/demo testing.
`DriverAccountAuthorizationTests.cs` replaces the real Auth dependency with a
fake HTTP handler (`ActiveDriverAuthHandler`) inside the test factory, so it
does not need `auth-service` running.

## 2 Prepare the live environment for the browser tests

Like the Sprint 1 Selenium tests (and unlike Sprint 3/4), these are **not**
gated behind an opt-in environment variable — they run as soon as the test
project runs, and Chrome opens visibly by default. They need
`FleetVehicleService` and `AuthUserService` reachable (the Vehicle pages sit
behind a login) and the frontend at `http://localhost:5173`:

```powershell
cd frontend
npm run dev
```

```powershell
dotnet test tests/Frontend.SeleniumTests/Frontend.SeleniumTests.csproj --filter "FullyQualifiedName~VehicleCreatePageTests|FullyQualifiedName~VehicleListPageTests|FullyQualifiedName~VehicleUpdatePageTests|FullyQualifiedName~VehicleDeletePageTests" --logger "trx;LogFileName=Sprint02-ui.trx" --results-directory "TestResults/QA-Sprint02/browser"
```

These share the `"Selenium collection"` xUnit collection with the other
page-test classes, so they run serially against one Chrome instance at a
time.

## 3 Run the performance test (SCRUM-79)

`FleetVehiclePerformanceTest.jmx` is **not wired to a fixture script** the way
the Sprint 3/4 plans are (`prepare_trip_fixtures.py`,
`prepare_tracking_fixtures.py` — there is no Fleet equivalent). Its HTTP
Header Manager hardcodes `Authorization: Bearer YOUR_TOKEN_HERE` — you must
obtain a real JWT and substitute it manually (either by editing the plan in
the JMeter GUI, or passing `-Jqa.token=<jwt>` after editing the plan to use
that property instead of the hardcoded header) before a real run will do
anything but fail every request with 401.

Install Java and Apache JMeter 5.6.3, with `jmeter` on PATH, then run from the
repository root once the token is set:

```powershell
jmeter -n -t tests/performance/FleetVehiclePerformanceTest.jmx -l tests/performance/results/fleet.jtl -e -o tests/performance/results/fleet-html
```

The HTML output folder must be empty or not exist. The plan's own comment
states the NFR target: under 2 seconds per request under load. No automated
pass/fail threshold is configured in the plan itself — check the JTL and
report manually, the same caveat the Sprint 3 README raises for its own plan.

## Not yet accounted for by this README

- No controlled Fleet-unavailable/timeout/malformed-response tests exist
  against the real Auth service (only the faked `ActiveDriverAuthHandler`
  path is covered) — the Sprint 4 README raises the mirror-image gap
  (Fleet-down handling) for Tracking; the same kind of gap exists here.
- The JMeter plan needs a fixture-preparation script and a scripted way to
  obtain the bearer token before it can be run repeatably or in CI, matching
  the pattern the Sprint 3/4 plans already use.
- `UnitTest1.cs` should be deleted rather than left as dead scaffold code.