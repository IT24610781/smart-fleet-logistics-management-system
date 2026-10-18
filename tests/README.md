# Smart Fleet testing

## SE3112: Unit Testing & Mocking

Owner: IT24610781 (I.D.P. Ashen). The project already contained team unit, integration,
Selenium and performance tests. The contribution below extends that foundation;
existing tests are not claimed as newly authored work.

Prerequisite: .NET SDK 10 (see global.json). Use PowerShell in the repository root.

```powershell
.\scripts\test-unit-mocking.ps1
```

The runner selects `Category=Unit` from the four API test projects, builds as needed,
and produces a fresh `TestResults/unit-mocking/<run>/` folder containing four TRX files
and `summary.md`. After the first build, use `-NoBuild` for the short live demo.
Restore may require internet on the first run. The selected tests themselves need no
Docker, MySQL, running APIs, Azure, Kafka, or email provider. No real email is sent.
A failure, skipped unit case, missing report, or zero executed tests makes the runner fail.

### Contribution map

| Service | Files and contribution | Controlled dependency / assertion |
|---|---|---|
| Auth | `AuthUserService.Tests/PasswordEmailSenderTests.cs`: new configuration, request and provider-error cases | HTTP handler stub/spy: exact request, one call, configuration rejection before HTTP, 401/429/500/503 propagation |
| Fleet | `FleetVehicleService.Tests/VehicleServiceUnitTests.cs`: new status and deletion cases; production repository interface | Repository fake, event spy and eligibility stub: correct transitions, save before publish, no event after save failure, no deletion when Trip eligibility fails |
| Trip | `TripDispatchService.Tests/DispatchWorkflowTests.cs`: new candidate matrix, invalid-capacity and dependency-failure cases; reused existing tests/fakes | Fleet stub/spy: exact capacity boundary, unit/status filtering, forwarded authorization, no calls on invalid input and no writes on failure; existing ranking test retained |
| Tracking | `TrackingTelemetryService.Tests/TrackingWorkflowTests.cs`: new history scope/limit/cancellation and failure cases | Trip stub and repository spy: limits 1/1000, caller context, no read after Trip failure, repository error must not look like empty history |
| Tracking baseline | `TrackingTelemetryService.Tests/SubmitLocationBoundaryTests.cs`: existing teammate tests, now classified `Category=Unit` | Retains parameterized lifecycle, ownership, coordinate and time cases; no duplicate implementation |

The current `Category=Unit` selection includes the entire existing DispatchWorkflowTests,
TrackingWorkflowTests and SubmitLocationBoundaryTests classes, plus the two new classes.
It is a documented selected suite, not an inventory of every unit test in the repository.

### How the test doubles work

- The system under test is the real service class.
- A stub supplies a configured response or exception instead of contacting a dependency.
- A fake repository provides a small in-memory substitute.
- A spy records arguments, calls, writes or events so assertions can inspect them.
- `[Theory]` runs a case for each `[InlineData]` row; `[Fact]` runs a single scenario.
- Each case creates its own mutable test doubles. No shared real database is involved.
- Moq/NSubstitute are not dependencies. Hand-written doubles satisfy the assignment's mocks/stubs requirement.
- Mocked tests validate service decisions; they cannot prove SQL correctness, network connectivity,
  actual email delivery or a real distributed workflow. Those need other test layers.

### Fleet production change

`VehicleService` now accepts `IVehicleRepository`. `VehicleRepository` implements that
interface, and Program.cs registers `IVehicleRepository -> VehicleRepository` as scoped.
The same MySQL SQL methods still execute in the application. Endpoint routes, DTOs and
business rules are unchanged. Unit tests supply `RecordingRepository` directly.
The repository has six operations: create, read by registration, read by ID, update,
list and delete. Unexpected fake operations throw instead of silently returning defaults.

### CI and integration tests

In GitHub Actions -> Smart Fleet CI Pipeline -> Build & Test (.NET), open:

1. `Unit testing and mocking (four APIs, no external dependencies)`.
2. The job summary showing the four suites.
3. Run summary -> Artifacts -> `unit-mocking-results` (TRX and Markdown).
4. The existing Auth, Fleet and remaining-project test steps for broader coverage.

The unit step uses the same script with `-NoBuild`. The build job also provisions a
separate MySQL service for the existing integration tests; the selected unit tests do
not use it. Upload runs even when unit tests fail, to preserve available evidence.
Tests run on feature pushes and pull requests. Azure deployment remains gated to dev.

Auth's CustomWebApplicationFactory endpoint/recovery/database tests need an isolated
`fleet_identity_db_test` schema. Fleet's CustomWebApplicationFactory endpoint tests need
`fleet_vehicle_db_test`. Configure `ConnectionStrings__DefaultConnection` for each test
process separately. Never point these tests at the deployed application databases.
Trip's `DriverAssignmentGuardDatabaseTests` are labeled `Category=Integration` and need
`TEST_TRIP_GUARD_CONNECTION` with create/drop permissions on a disposable MySQL server.
That custom attribute already skips correctly on the fetched dev baseline when the
setting is absent; no second fix was necessary. CI explicitly supplies the setting.
Tracking's `Category=LiveApi` cases are separately opt-in through `QA_RUN_LIVE_API=1`.
Selenium tests belong to `Frontend.SeleniumTests`, not this isolated runner.

## Two-minute individual demo

Prepare once by running the script normally so packages are restored and projects built.
Keep the terminal, Fleet test file, Trip parameterized test, and a successful Actions run open.

- 0:00-0:20: Explain the four APIs and that unit tests replace their external dependencies.
- 0:20-0:45: Run `.\scripts\test-unit-mocking.ps1 -NoBuild`; show the four result rows.
- 0:45-1:15: Show `SuggestVehicles_CandidateRules_UseControlledFleetResponse`: one theory,
  multiple inputs, a real TripService and a fake Fleet client.
- 1:15-1:45: Show `SaveFailure_DoesNotPublishSuccessfulStatusChange`: the repository throws;
  the test passes because the real service propagates the error and publishes no event.
- 1:45-2:00: Show the Actions unit step/artifact and explain your additions versus existing team tests.

Do not equate a passing mocked test with a successful real Azure deployment. Be ready to
explain why zero writes is an assertion, why a failing dependency is expected in a passing
test, and why interface injection makes the service independently testable.

## Assignment submission

The group must select exactly four distinct roles. This contribution is Unit Testing & Mocking.
Coordinate the other three roles with teammates. Submit source code, tests, configurations
and a README with setup, commands and ownership, together with **up to five slides OR a
one-page architecture PDF**. The presentation is 12 minutes: group introduction/automation
2 minutes, four individual demos 8 minutes total, and viva 2 minutes.

See [SE3112-verification.md](SE3112-verification.md) for measured results and their limits.
