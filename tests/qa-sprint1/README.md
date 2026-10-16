# Sprint 1 QA automation

Scope: US-01 Admin Creates User Account, US-02 User Login with JWT, US-03 User
Logout, and US-04 View and Edit Profile - the core AuthUserService stories. This
is a catalogue of the existing AuthUserService coverage, in the same format as
the Sprint 3 and Sprint 4 QA READMEs; it does not add new test files.

US-08 (Dispatcher self-registration) also lives in `AuthUserService.Tests` but
is already documented in `../qa-sprint3/README.md` — it is not repeated here.

Two files in this area are not functional story coverage and are called out
separately at the bottom: `DriverDeactivationTests.cs` /
`DriverDeactivationDatabaseTests.cs` and `PasswordRecoveryTests.cs` cover real
features, but neither is labelled with a US number in code or in the docs this
README could check against — confirm the right story ID with the team before
citing these against a specific requirement.

## Included code

| File | Coverage |
| --- | --- |
| `../AuthUserService.Tests/CreateDriverEndpointTests.cs` | Seven US-01 endpoint checks: Admin creates a Driver account via the real API |
| `../AuthUserService.Tests/CreateUserValidationTests.cs` | Eight US-01 invalid/missing-field checks before database access |
| `../AuthUserService.Tests/LoginEndpointTests.cs` | Ten US-02 endpoint checks: real login, JWT issuance, role claims |
| `../AuthUserService.Tests/LoginValidationTests.cs` | Two US-02 input-validation checks |
| `../AuthUserService.Tests/LogoutArchitectureTests.cs` | Three US-03 checks on the stateless-logout design (no server-side revocation) |
| `../AuthUserService.Tests/ProfileEndpointTests.cs` | Eight US-04 endpoint checks: authenticated read/update of a user's own profile |
| `../AuthUserService.Tests/UpdateProfileValidationTests.cs` | Six US-04 invalid-update checks (Scenario 3) |
| `../Frontend.SeleniumTests/CreateDriverPageTests.cs` | Five US-01 live browser scenarios |
| `../Frontend.SeleniumTests/LoginPageTests.cs` | Five US-02 live browser scenarios, including the real login form end-to-end |
| `../Frontend.SeleniumTests/LogoutPageTests.cs` | Two US-03 live browser scenarios |
| `../Frontend.SeleniumTests/ProfilePageTests.cs` | Six US-04 live browser scenarios |
| `../Frontend.SeleniumTests/TestDataSeeder.cs` | Shared helper: mints a short-lived Admin JWT from local `appsettings.Development.json` to seed a fresh test Driver, since there is no UI path to create the first account |
| `../Frontend.SeleniumTests/AdminSessionInjector.cs` | Shared helper: writes a signed Admin JWT into browser `localStorage` to reach Admin-only pages without a real Admin login form |

Run the commands below in PowerShell from the repository root. .NET 10 is required.

## 1 Run the unit/integration checks

These hit the real `AuthUserService` app in-memory (`WebApplicationFactory`) against
a real MySQL schema — not a mock — so a reachable test database is required first.

`CustomWebApplicationFactory.cs` defaults to:

```
Server=127.0.0.1;Port=3306;Database=fleet_identity_db_test;Uid=root;Pwd=test_root_password;
```

That default assumes a plain local MySQL install, **not** the `docker-compose`
`auth-db` container (which listens on host port `3307` with password
`rootpassword`, per `docker-compose.yml`). To run these against the containers
you already have up, create a separate test schema there and point the tests
at it with an environment variable override instead of editing the constant:

```powershell
# One-time: create the test schema against the existing auth-db container
Get-Content database/auth-user/schema.sql | docker exec -i smart-fleet-logistics-management-system-auth-db-1 mysql -uroot -prootpassword -e "CREATE DATABASE IF NOT EXISTS fleet_identity_db_test;"
Get-Content database/auth-user/schema.sql | docker exec -i smart-fleet-logistics-management-system-auth-db-1 mysql -uroot -prootpassword fleet_identity_db_test

$env:ConnectionStrings__DefaultConnection = "Server=127.0.0.1;Port=3307;Database=fleet_identity_db_test;Uid=root;Pwd=rootpassword;"

dotnet test tests/AuthUserService.Tests/AuthUserService.Tests.csproj --filter "FullyQualifiedName~CreateDriverEndpointTests|FullyQualifiedName~CreateUserValidationTests|FullyQualifiedName~LoginEndpointTests|FullyQualifiedName~LoginValidationTests|FullyQualifiedName~LogoutArchitectureTests|FullyQualifiedName~ProfileEndpointTests|FullyQualifiedName~UpdateProfileValidationTests" --logger "trx;LogFileName=Sprint01-unit.trx" --results-directory "TestResults/QA-Sprint01/unit"
```

Tests insert and read real rows in whatever database the connection string
points to — never run this against a database you use for manual/demo testing.

## 2 Prepare the live environment for the browser tests

Unlike the Sprint 3/4 browser suites, these Selenium tests are **not** gated
behind an opt-in environment variable (there is no `QA_RUN_SPRINT1_UI` flag) —
they run as soon as the test project runs, and Chrome opens visibly by default
(no `--headless` flag is set; add one in `SeleniumTestBase.cs` for CI).

They also expect `AuthUserService` reachable at `http://localhost:5100`
(`TestDataSeeder.cs` / `AdminSessionInjector.cs`) and the frontend at
`http://localhost:5173` (`SeleniumTestBase.cs`) — both hardcoded constants, not
environment variables, so there is nothing to export; just make sure the ports
match your running setup. `docker-compose`'s `auth-service` already publishes
`5100`, so if your containers are up this lines up without changes:

```powershell
cd frontend
npm run dev
```

Keep a separate terminal for the frontend; run the tests from the repo root in
another.

**Known gap, not a test bug:** `TestDataSeeder.cs` and `AdminSessionInjector.cs`
both note there is no seeded Admin account and no self-registration path for
Admin — the only way to create any account is the Admin-only
`POST /api/users/drivers` endpoint, which itself needs an existing Admin. Both
helpers work around this by minting a JWT directly rather than logging in
through the UI. If `AdminSeeder` (which seeds a default `admin` /
`Admin123!` account on a fresh database — see `../qa-sprint4/README.md` for
how this was found) is now wired up, re-check whether these two helpers are
still necessary, or whether they can be simplified to use the seeded account
instead.

## 3 Run the Sprint 1 browser scenarios

```powershell
dotnet test tests/Frontend.SeleniumTests/Frontend.SeleniumTests.csproj --filter "FullyQualifiedName~CreateDriverPageTests|FullyQualifiedName~LoginPageTests|FullyQualifiedName~LogoutPageTests|FullyQualifiedName~ProfilePageTests" --logger "trx;LogFileName=Sprint01-ui.trx" --results-directory "TestResults/QA-Sprint01/browser"
```

These all share the `"Selenium collection"` xUnit collection with the other
page-test classes in the project, so they run serially against one Chrome
instance at a time — do not try to parallelize them.

## 4 Performance

There is no JMeter plan for AuthUserService in `tests/performance/` at this
time (`FleetVehiclePerformanceTest.jmx`, `TripDispatchPerformanceTest.jmx` and
`TrackingPerformanceTest.jmx` exist; an Auth equivalent does not). If load
targets are required for login/account-creation, that plan still needs to be
written — this README does not claim coverage that does not exist.

## Not yet accounted for by this README

- `DriverDeactivationTests.cs` (nine checks) and
  `DriverDeactivationDatabaseTests.cs` (one check) cover a real, documented
  feature (`../../docs/DRIVER_DEACTIVATION.md`) but carry no US number in
  code. Confirm the story ID with the team before citing these as coverage
  for a specific requirement.
- `PasswordRecoveryTests.cs` (eleven checks, as `PasswordSecurityTests`) is in
  the same position — real coverage, no US label found.
- No controlled-failure tests exist here for what happens when the database
  itself is unreachable at login/create-user time (only validation and
  happy-path/role-mismatch cases are covered).
