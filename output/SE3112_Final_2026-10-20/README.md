# Smart Fleet testing guide

We are a three-person team building a fleet and logistics system. This README explains what we tested, how to run the checks, and what our results show. It is written for someone opening the project for the first time.

The project copy includes our four APIs, React frontend, databases, Docker setup, tests and GitHub Actions workflow. It also includes Jananjana's completed browser-test contribution so all three members' work can be reviewed together. Those Selenium tests are still on her feature branch and have not yet been merged into `dev`.

Use this guide alongside the five-slide presentation. The team ownership and commands below describe the code included with this project copy; the test result sections distinguish checks that have run from those that still need a live browser or load-test run.

## 1. Team and individual ownership

| Member | Individual role | Contribution and code |
|---|---|---|
| **I.D.P. Ashen — IT24610781** | Unit Testing & Mocking | Added 48 focused tests across the four APIs, made Fleet's vehicle service easier to test without MySQL, and added a script and CI report for running these tests. |
| **Jananjana J.W.C — IT24104006** | UI / E2E Testing | Built the Selenium browser suite for account flows, vehicles, trips, tracking, reports and access control. It uses real browser journeys and controlled failure cases. |
| **Ranjula Adikari — IT24104259** | Load & Performance Testing | Added JMeter plans and a repeatable Fleet stress test that signs in each test thread and measures the vehicle create flow. |

The project already contained team tests. Suite totals describe maintained coverage and do not claim every existing test as new individual authorship. Database-backed integration tests, infrastructure and GitHub Actions are shared project work. We do not assign a fourth individual role.

## 2. System architecture

React/Vite serves the browser interface. Four ASP.NET Core APIs handle Auth/User, Fleet/Vehicle, Trip Dispatch and Tracking/Telemetry. Each service owns its MySQL database. Trip checks Fleet eligibility, Tracking validates Trip assignment, and JWTs carry authenticated identity. Docker Compose provides local services, databases and Kafka.

| Component | Source folder | Local API/UI | Local MySQL port and database |
|---|---|---|---|
| Auth / User | `services/AuthUserService/` | `http://localhost:5100` | `3307`, `fleet_identity_db` |
| Fleet / Vehicle | `services/FleetVehicleService/` | `http://localhost:5041` | `3317`, `fleet_vehicle_db` |
| Trip Dispatch | `services/TripDispatchService/` | `http://localhost:5051` | `3327`, `trip_dispatch_db` |
| Tracking / Telemetry | `services/TrackingTelemetryService/` | `http://localhost:5000` | `3337`, `tracking_telemetry_db` |
| Frontend | `frontend/` | `http://localhost:5173` | No separate frontend database |

In Azure the four APIs use Container Apps. The frontend uses App Service. The GitHub workflow builds API container images and pushes them to the shared Azure Container Registry. The frontend follows its own build-and-ZIP deployment path.

## 3. Tools and first setup

Use PowerShell from the extracted source root. Commands below assume Windows unless stated otherwise.

| Tool | Required for | Version/configuration |
|---|---|---|
| .NET SDK | API builds, xUnit and Selenium | `10.0.400`, as pinned in `global.json` |
| PowerShell | Unit runner and commands | PowerShell 7 recommended |
| Node.js and npm | Frontend | Node 24 matches CI, use the committed npm lockfile |
| Docker Desktop / Compose v2 | Local APIs, MySQL and Kafka | Linux containers |
| Chrome | Browser tests | Current Chrome compatible with Selenium Manager/ChromeDriver |
| Java | JMeter | Java 17 or later |
| Apache JMeter | Load tests | 5.6.3 or later; add its `bin` folder to PATH |
| Python 3 | Optional Trip/Tracking fixture generation | See the fixture scripts in the JMeter folder |

Check the tools you need:

```powershell
dotnet --version
node --version
npm --version
docker compose version
java -version
Get-Command jmeter.bat
```

Restore and build from the source root:

```powershell
dotnet restore SmartFleet.slnx
dotnet build SmartFleet.slnx --no-restore
```

The first restore may need internet access. Unit execution itself needs no running external service.

## 4. Local application for UI and load tests

Start Docker Desktop. In terminal A, create a development-only signing key, then build/start the stack:

```powershell
$env:FLEET_JWT_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
docker compose up --build -d
docker compose ps
```

All four APIs must use the same key, issuer and audience configuration. Compose supplies these consistently. Keep this terminal's key available for any Compose commands that recreate containers during the session. There is no `.env.example` in this snapshot. A local `.env` file is optional; it is not required by the commands above.

In terminal B, explicitly route Vite to the local APIs. This avoids the committed production URL file directing a local browser to Azure:

```powershell
$env:VITE_API_BASE_URL = 'http://localhost:5100'
$env:VITE_VEHICLE_API_URL = 'http://localhost:5041'
$env:VITE_TRIP_API_URL = 'http://localhost:5051'
$env:VITE_TRACKING_API_URL = 'http://localhost:5000'
Set-Location frontend
npm ci
npm run dev -- --port 5173 --strictPort
```

Keep the frontend on port 5173: the older Selenium base class fixes that address, although newer Sprint 3/4 classes support `QA_FRONTEND_URL`.

Check local API readiness in another terminal:

```powershell
foreach ($port in 5100, 5041, 5051, 5000) {
    Invoke-WebRequest "http://localhost:$port/health/ready" | Select-Object StatusCode
}
```

Auth seeds an `admin` account when the database has none. `services/AuthUserService/Data/AdminSeeder.cs` defines its development behavior and the `BootstrapAdmin:Password` setting. A reused database may contain a different password. Use the actual **local test** admin account for Selenium and JMeter. Do not use Azure production/demo credentials in submitted files.

Stop services with `docker compose down`. Adding `-v` deletes database volume data and is not needed for ordinary shutdown.

## 5. Unit testing and mocking — Ashen

### 5.1 Run the selected suite

From the root:

```powershell
.\scripts\test-unit-mocking.ps1
```

After the first build:

```powershell
.\scripts\test-unit-mocking.ps1 -NoBuild
```

The script runs `Category=Unit` across the four API test projects. It writes four TRX files and `summary.md` to a fresh `TestResults/unit-mocking/<run>/` folder. It fails on a failed/skipped selected case, missing result file or zero executed tests. This suite needs no Docker, MySQL, Azure, Kafka, real Fleet/Trip endpoint or Brevo account.

### 5.2 What the new cases prove

| Service | Main file | Controlled dependency and assertions |
|---|---|---|
| Auth | `tests/AuthUserService.Tests/PasswordEmailSenderTests.cs` | HTTP handler double. Missing configuration prevents HTTP, the request has the correct recipient/code, and provider errors propagate. No real email is sent. |
| Fleet | `tests/FleetVehicleService.Tests/VehicleServiceUnitTests.cs` | Repository fake, event spy and Trip eligibility stub. Valid status changes save before publishing, invalid transitions produce no write/event, save failure produces no successful event, and deletion respects eligibility. |
| Trip | `tests/TripDispatchService.Tests/DispatchWorkflowTests.cs` | Fake Fleet client. Capacity boundaries, incompatible units and unavailable vehicles produce the expected suggestions. Invalid input makes no Fleet call. Dependency failure makes no write. |
| Tracking | `tests/TrackingTelemetryService.Tests/TrackingWorkflowTests.cs` | Fake Trip client and repository spy. History limits/caller scope, cancellation and dependency failures behave correctly. Existing boundary/lifecycle tests remain part of the selected suite. |

`VehicleService` accepts `IVehicleRepository` so tests can supply the fake. Production dependency injection still selects the existing MySQL repository. xUnit `[Theory]` and `[InlineData]` supply multiple cases. The doubles are hand-written fakes, stubs and spies; the project does not use Moq or NSubstitute.

### 5.3 Recorded and rechecked evidence

| API | Selected tests | New cases in the SE3112 contribution |
|---|---:|---:|
| Auth | 12 | 12 |
| Fleet | 19 | 19 |
| Trip | 22 | 13 |
| Tracking | 79 | 4 |
| **Total** | **132** | **48** |

The original verification on 18 October recorded 132 passes, zero failures and zero skips. **The project copy passed the same 132 selected cases again on 20 October**, with zero failures and zero skips. Fresh TRX files and a summary are included in `submission/verification/unit-mocking/`. The other 84 selected cases were already part of the team project. `tests/SE3112-verification.md` also records a separate broader API regression run of 392 passes and 18 opt-in skips. Those 18 skips are not passes. These are test counts, not a code-coverage percentage.

For the viva, show one `[Theory]` in Trip and Fleet's `SaveFailure_DoesNotPublishSuccessfulStatusChange` case. Explain that a passing failure-path test means the service handled the simulated failure as expected.

## 6. UI / end-to-end testing — Jananjana

### 6.1 Verified suite inventory

The completed branch compiles and .NET test discovery lists **82 cases across 19 classes**. The build reports three nullable-dictionary warnings in the geolocation CDP calls. Compilation/discovery do not execute the browser journeys. The branch README has no completed final run table, so this package makes no claim of 82 passing browser tests.

| Area | Cases |
|---|---:|
| Authentication, profile and driver accounts | 18 |
| Vehicle CRUD | 15 |
| Trips and dispatch | 2 |
| Tracking and location | 14 |
| Reports and access control | 33 |
| **Total** | **82** |

The suite uses real Chrome, real application login and API-created fixtures. It serializes execution through `NoParallelization.cs`. Explicit waits reacquire React elements after stale references. Sprint 4 tests save screenshots on success/failure. Geolocation cases use Chrome DevTools overrides. Some failure cases replace browser functions or HTTP behavior to exercise an unavailable dependency, so describe those as controlled failure tests rather than proof of a real infrastructure outage.

### 6.2 Set local credentials without putting the password in command history

In terminal C, from the root of the source package:

```powershell
$credential = Get-Credential -UserName admin -Message 'Local Smart Fleet test admin'
$env:QA_ADMIN_USERNAME = $credential.UserName
$env:QA_ADMIN_PASSWORD = $credential.GetNetworkCredential().Password
$env:QA_RUN_SPRINT3_UI = '1'
$env:QA_FRONTEND_URL = 'http://localhost:5173'
$env:QA_AUTH_URL = 'http://127.0.0.1:5100'
$env:QA_FLEET_URL = 'http://127.0.0.1:5041'
$env:QA_TRIP_URL = 'http://127.0.0.1:5051'
$env:QA_TRACKING_URL = 'http://127.0.0.1:5000'
$env:QA_SELENIUM_HEADLESS = '0'
$env:QA_OUTPUT_ROOT = Join-Path (Get-Location) 'TestResults/selenium-evidence'
```

The actual opt-in attribute checks `QA_RUN_SPRINT3_UI`, including for Sprint 4 tests. `QA_RUN_SPRINT4_UI` is not required by the checked-in attribute. `QA_SELENIUM_HEADLESS` applies to Sprint3/4-derived fixtures; the older `SeleniumTestBase` currently creates a visible browser regardless of that variable.

### 6.3 Discover, run and filter

```powershell
# Discovery builds the project but does not launch browsers.
dotnet test tests/Frontend.SeleniumTests --list-tests

# Full suite against the running local application.
dotnet test tests/Frontend.SeleniumTests --logger "trx;LogFileName=selenium.trx" --results-directory TestResults/selenium

# One short access-control example for the live demonstration.
dotnet test tests/Frontend.SeleniumTests --no-build --filter "FullyQualifiedName~ReportsPageTests.RP_02_Dispatcher_IsBlockedFromReportsPage" --logger "trx;LogFileName=selenium-demo.trx" --results-directory TestResults/selenium-demo
```

The full suite creates real test accounts, vehicles and trips. Run it against a dedicated local test stack. The `RP_06` authorization probe in `ReportsPageTests` deliberately checks for a suspected authorization defect; investigate any failure instead of filtering it out to produce a green total. `VehicleUpdatePageTests` also includes an explicitly named known-gap case.

The original `run-selenium-demo.cmd` has an absolute `C:\Users\chamo\...` directory and uses `--no-build`. Use the portable commands above from the extracted source root. Its runner is preserved as contribution evidence, not silently rewritten.

### 6.4 Evidence after a real run

Keep `TestResults/selenium/selenium.trx`, the console summary, and the screenshots under `QA_OUTPUT_ROOT`. Record the environment, run date, passed/failed/skipped counts, source commit and any failed test IDs. A skip caused by a missing opt-in setting is not a pass.

## 7. Load and performance testing — Ranjula

`tests/Performance.JmeterTests/` contains four original API plans and the new `FleetVehicleStressTest.jmx`. The Fleet stress plan is the preferred SE3112 demonstration because it automatically logs in once per thread, extracts the token and sends it with the test requests.

| Thread group | Concurrency | Ramp-up | Loops | Business samples |
|---|---:|---:|---:|---:|
| Baseline | 10 | 5 seconds | 5 | 150 |
| Stress spike | 60 | 2 seconds | 3 | 360 |

The groups exercise create/list/update. Duration assertions target under 2000 ms and response assertions check expected status codes. JMeter can therefore count an HTTP success as an assertion failure if it exceeds the time target. An error percentage is a sample/assertion failure rate, not automatically the percentage of HTTP 500 responses.

### 7.1 Portable non-GUI command

Start the local Auth/Fleet APIs and their databases, and set `QA_ADMIN_PASSWORD` in the same terminal using the credential prompt in section 6. Then:

```powershell
$jmeterCommand = (Get-Command jmeter.bat -ErrorAction Stop).Source
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$results = Join-Path (Get-Location) "TestResults/jmeter/$runId"
New-Item -ItemType Directory -Path $results -Force | Out-Null
& $jmeterCommand -n -t tests/Performance.JmeterTests/FleetVehicleStressTest.jmx -l "$results/results.jtl" -e -o "$results/dashboard"
if ($LASTEXITCODE -ne 0) { throw 'JMeter execution failed; inspect its log.' }
```

Open `TestResults/jmeter/<run>/dashboard/index.html`. Check the Statistics table, response-time percentiles, throughput and assertion errors. JMeter's process exit code alone does not prove that all performance targets passed; inspect the generated sample results.

The original `.bat` runners remain in the source. They include a teammate-specific JMeter fallback path and password-as-argument handling. The direct command above finds JMeter through PATH and uses the stress plan's `QA_ADMIN_PASSWORD` environment variable. It avoids those portability assumptions.

### 7.2 Documented performance finding

The committed JMeter README reports a Docker-stack run, not a fresh Azure run:

| Fleet create metric | Baseline | Stress spike |
|---|---:|---:|
| Failed samples | 6.00% | 16.67% |
| Median | 74.5 ms | 519 ms |
| p95 | 2992 ms | 4600 ms |

The plan recorded 510 business samples and 6.47% errors overall. The spike exposed degradation, and both create p95 measurements exceed 2 seconds. The result supports investigating the create path under concurrency. It does not identify a confirmed database-lock or pool-exhaustion root cause.

The older broader-plan measurements are documented in `tests/Performance.JmeterTests/README.md`. Trip/Tracking need fresh fixture files. `prepare_trip_fixtures.py` and `prepare_tracking_fixtures.py` generate them against the test stack. `run-all-jmeter-tests.bat` provides the longer combined demonstration; review its paths and fixtures before using it. The legacy Fleet performance plan contains a placeholder token, so use the new authenticated stress plan for the timed demonstration.

## 8. Shared integration tests

API endpoint/database tests live in the four API test projects beside unit tests. GitHub Actions creates isolated Auth and Fleet schemas on a disposable MySQL service. Unit tests do not need that MySQL service.

For a local integration run, provision a **disposable MySQL test server** and load:

- `database/auth-user/schema.sql` into `fleet_identity_db_test` after changing the database name.
- `database/fleet-vehicle/schema.sql` into `fleet_vehicle_db_test` after changing the database name.

Set `ConnectionStrings__DefaultConnection` per test process for the corresponding isolated schema. Trip's guard database tests use `TEST_TRIP_GUARD_CONNECTION` and require create/drop permissions on the disposable server. Never give that setting a shared or deployed database connection.

```powershell
# After configuring the appropriate isolated schema connection for each process:
dotnet test tests/AuthUserService.Tests --no-build
dotnet test tests/FleetVehicleService.Tests --no-build
dotnet test tests/TripDispatchService.Tests --no-build
dotnet test tests/TrackingTelemetryService.Tests --no-build
```

Tracking's `LiveApi` category is separately opt-in. The selected unit script cannot establish database correctness, browser behavior or actual cloud availability. Read `tests/README.md` for the technical integration details, but use this root README for the lecturer-approved three-person SE3112 role allocation.

## 9. GitHub Actions and pipeline evidence

Workflow: `.github/workflows/build.yml`, displayed as **Smart Fleet CI Pipeline**.

| Stage | Trigger and execution | Evidence |
|---|---|---|
| .NET build/tests | Feature/dev/main pushes and PRs to dev/main. Restore/build, selected units and four API project suites. | Actions logs, four-service summary, `unit-mocking-results` artifact |
| Frontend lint/build | Same validation triggers. Node 24, `npm ci`, lint, build. | Frontend job log |
| Docker Build Check | Same validation triggers. Publish/build four API images, scan them. | Build and Anchore scan output |
| Five deployment jobs | Push to `dev` after required validation. Auth, Fleet, Trip, Tracking, Frontend. | Five deployment boxes, image/revision summaries |
| Hosted smoke checks | After deployment. Frontend `/login`, four `/health/ready` URLs, authenticated workflow. | Frontend and Post-deployment workflow smoke logs |
| Selenium | Manual/local today. Not invoked by this workflow. | Browser TRX and screenshots from a live run |
| JMeter | Manual/on-demand today. Not invoked by this workflow. | JTL and HTML dashboard |

The three fast validation jobs run in parallel. The Azure jobs then run sequentially in the order coded by `needs`. PR runs validate code and skip deployment. A single workflow file does not mean a single Azure application.

The image scans use `fail-build: false`. Findings are visible, but a scan finding does not currently block deployment. Do not describe the pipeline as enforcing a vulnerability threshold. Selenium and JMeter use the shared project and CLI tools but are not yet automatic required CI gates. This remains an integration improvement for the team rather than a completed feature.

Frontend checks can also run locally:

```powershell
Set-Location frontend
npm ci
npm run lint
npm run build
```

For remote unit evidence, open GitHub → Actions → Smart Fleet CI Pipeline → a run → Build & Test (.NET). Expand **Unit testing and mocking (four APIs, no external dependencies)**. On the run summary, download **unit-mocking-results**. A branch's successful .NET build does not imply that live Selenium or JMeter ran.

Cloud access uses Azure OIDC and configured GitHub secrets for client/tenant/subscription identifiers. API secrets and connection strings belong in Azure configuration, not slides, test reports or the source ZIP. The frontend's committed `.env.production` contains public API URLs and is included intentionally.

## 10. Troubleshooting and evidence boundaries

| Symptom | First check |
|---|---|
| Unit discovery/restore failure | SDK 10.0.400, NuGet access and a successful build |
| Selenium skips | `QA_RUN_SPRINT3_UI=1` in the same terminal |
| Selenium connection refused | All four APIs and Vite are running on the table's local ports |
| Selenium 401 or invalid audience | Actual local credentials and matching JWT settings across APIs |
| UI talks to Azure during a local run | Set all four `VITE_*` variables before starting Vite |
| Chrome/driver startup failure | Installed Chrome, Selenium Manager network access, driver compatibility |
| JMeter missing | Java and JMeter `bin` on PATH, `Get-Command jmeter.bat` |
| JMeter reports failures | Inspect response/status assertions and latency separately; refresh fixtures |
| Azure API root returns 404 | Use an implemented route such as `/health/ready`; a base URL is not necessarily a web page |

After live tests, remove local secret variables if the terminal will remain open:

```powershell
Remove-Item Env:QA_ADMIN_PASSWORD -ErrorAction SilentlyContinue
Remove-Variable credential -ErrorAction SilentlyContinue
```

The submitted source omits machine `.env` files, tokens, credential files, `.git`, build output, `node_modules`, unrelated local result folders and private build artifacts. The selected fresh unit TRX reports are included explicitly as submission evidence. Development defaults committed in the original Docker configuration remain development defaults, not cloud credentials.

## 11. Source map and demonstration order

```text
services/                       Four application APIs
frontend/                       React/Vite application
database/                       Database schemas
tests/*Service.Tests/           API unit and integration tests
tests/Frontend.SeleniumTests/   Jananjana's assembled Selenium suite
tests/Performance.JmeterTests/  Ranjula's JMeter plans and runners
scripts/test-unit-mocking.ps1   Ashen's selected unit runner
scripts/test-azure-workflow.ps1 Authenticated hosted smoke workflow
.github/workflows/              GitHub Actions pipelines
.github/actions/                Shared Container App deployment action
deployment/                     Deployment configuration and notes
docker-compose.yml              Local containers and databases
global.json                     .NET SDK pin
SmartFleet.slnx                 Solution
submission/                     Test reports
```

Suggested presentation handover: start with the system and roles, Ashen demonstrates a theory/fake and unit output, Jananjana demonstrates a browser journey and its assertions, Ranjula explains baseline versus spike and the measured failure, then close with the shared CI/CD evidence. Agree exact speaking times with the lecturer for the three-person arrangement; do not reuse the original four-person timing unchanged.

## 12. Reference branches

- Unit/mocking: <https://github.com/IT24610781/smart-fleet-logistics-management-system/tree/feature/se3112-unit-mocking>
- Selenium: <https://github.com/IT24610781/smart-fleet-logistics-management-system/tree/feature/selenium-e2e-tests>
- JMeter: <https://github.com/IT24610781/smart-fleet-logistics-management-system/tree/feature/se3112-jmeter-load-testing>
- Shared dev workflow: <https://github.com/IT24610781/smart-fleet-logistics-management-system/blob/dev/.github/workflows/build.yml>


