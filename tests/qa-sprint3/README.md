# Sprint 3 QA automation

Scope: US08 Dispatcher registration, US09 trip creation, US10 suggestions, US11
assignment, US12 Driver response and US13 lifecycle visibility. These additions
extend the existing Sprint 2 backend, Selenium and JMeter tests.

## Included code

| File | Coverage |
| --- | --- |
| `../AuthUserService.Tests/DispatcherRegistrationValidationTests.cs` | Seven US08 invalid-field checks before database access |
| `../TripDispatchService.Tests/DispatchWorkflowTests.cs` | Nine US09–US12 checks using fake repository and Fleet dependencies |
| `../TripDispatchService.Tests/TripLifecycleTests.cs` | Existing US13 state and audit checks |
| `../Frontend.SeleniumTests/SprintDispatchWorkflowTests.cs` | 24 workbook UI scenarios in one sequential browser workflow |
| `../Frontend.SeleniumTests/Sprint3SeleniumTestBase.cs` | Opt-in browser fixture, actual login and headless Chrome |
| `../performance/TripDispatchPerformanceTest.jmx` | Eight measured business workflows |
| `../performance/prepare_trip_fixtures.py` | Fresh accounts, vehicles and trips for each performance run |

Run the commands below in PowerShell from the repository root. .NET 10 is required.
Existing Auth and Fleet integration suites remain available in their original projects.

## 1 Run the unit checks

These filters do not require running databases or application services.

```powershell
dotnet test tests/AuthUserService.Tests/AuthUserService.Tests.csproj --filter "FullyQualifiedName~DispatcherRegistrationValidationTests" --logger "trx;LogFileName=US08-validation.trx" --results-directory "TestResults/QA-Sprint03/unit/auth"
dotnet test tests/TripDispatchService.Tests/TripDispatchService.Tests.csproj --filter "FullyQualifiedName~DispatchWorkflowTests|FullyQualifiedName~TripLifecycleTests" --logger "trx;LogFileName=dispatch-unit.trx" --results-directory "TestResults/QA-Sprint03/unit/trip"
```

## 2 Prepare the live environment

Start the local Auth, Fleet and Trip services, their databases and the frontend.
Chrome must be installed for Selenium. The browser test does not start the application.
Use a test environment: the live workflows create accounts, vehicles and trips,
and retain those records for reproduction. Do not run browser and load tests together.

| Environment variable | Default or purpose |
| --- | --- |
| `QA_AUTH_URL` | `http://localhost:5100` |
| `QA_FLEET_URL` | `http://localhost:5041` |
| `QA_TRIP_URL` | `http://localhost:5051` |
| `QA_FRONTEND_URL` | `http://localhost:5173`; configure the frontend to call the same API hosts |
| `QA_ADMIN_USERNAME` | `admin` |
| `QA_ADMIN_PASSWORD` | Required; use the actual local Admin password |
| `QA_OUTPUT_ROOT` | Optional browser evidence directory; default is a timestamped `TestResults/QA-Sprint03/browser` folder |
| `QA_SELENIUM_HEADLESS` | Headless by default; set `0` to watch Chrome |
| `QA_RUN_SPRINT3_UI` | Set `1` to enable the live browser workflow |

```powershell
$env:QA_ADMIN_USERNAME = "admin"
$env:QA_ADMIN_PASSWORD = Read-Host "Enter your local QA Admin password"
```

Do not commit credentials or generated fixture JSON. Generated measurements and
screenshots belong in ignored `TestResults` directories or an external evidence folder.

## 3 Run the Sprint 3 browser workflow

```powershell
$env:QA_RUN_SPRINT3_UI = "1"
dotnet test tests/Frontend.SeleniumTests/Frontend.SeleniumTests.csproj --filter "FullyQualifiedName~SprintDispatchWorkflowTests" --logger "trx;LogFileName=Sprint03-ui.trx" --results-directory "TestResults/QA-Sprint03/browser-trx"
Remove-Item Env:QA_RUN_SPRINT3_UI
```

The workflow records each workbook row in `results/ui-results.json` and captures
screenshots under `results/ui-screenshots` inside the output directory. An unexpected
scenario fails the overall xUnit test after evidence collection; its TRX does not
silently pass a failed workbook scenario. Fixture setup failures abort the workflow.
Because the scenarios share setup and state, this is one orchestrated test, not 24
independent xUnit methods.

**Known expectation:** UI row 22 expects `Rejected`, as stated in the current workbook.
The service currently returns `Requested` and clears the assignment. This scenario
is expected to fail until the team confirms the requirement and updates the code or
test expectation accordingly. Do not change it solely to obtain a green run.

## 4 Prepare fresh JMeter data

Python 3 is required; the fixture helper uses only the standard library. JMeter 5.6.3
and a compatible Java runtime must be installed and available on PATH.

```powershell
$qaPerfDir = Join-Path (Get-Location) "TestResults/QA-Sprint03/performance"
New-Item -ItemType Directory -Force -Path $qaPerfDir | Out-Null
$qaFixtureFile = Join-Path $qaPerfDir "perf-fixtures.json"
python tests/performance/prepare_trip_fixtures.py --output "$qaFixtureFile" --threads 10 --loops 5
```

The helper creates dedicated Dispatcher and Driver accounts and 200 vehicles for
the default 10-thread, five-loop run, plus setup trips. It writes the generated test
account password and IDs to the fixture file; it does not export the Admin password
or JWTs. Vehicles use `54321 kg` to separate them from passenger browser fixtures.
Prepare new data before every run because assignments consume fixture state.

## 5 Run JMeter and review measurements

```powershell
jmeter -n -t tests/performance/TripDispatchPerformanceTest.jmx "-Jqa.fixtures=$qaFixtureFile" "-Jqa.threads=10" "-Jqa.loops=5" "-Jqa.ramp=5" -l "$qaPerfDir/performance.jtl" -j "$qaPerfDir/jmeter.log"
```

Keep each dotted JMeter property argument quoted in PowerShell; otherwise it can split the property name. Match `qa.threads` and `qa.loops` to the fixture preparation arguments. The plan runs
US08, US09, US10, US11, US12, US13, SCRUM115 and MIXED sequentially, recording 50
workflow samples per group at the default settings. One sample may contain several
HTTP calls. Per-thread login and fixture creation are excluded from measured time.
Inspect the JTL success values as well as JMeter's process exit code.

US12's performance assertion observes the current implementation (`Requested` after
rejection); it does not establish that the workbook rejection acceptance rule is met.
No response-time acceptance threshold is configured. Agree the metric, threshold,
error percentage and workload before marking performance cases Pass or Fail.
The previous two-second Sprint 2 target has not been confirmed for Sprint 3.

## Evidence and remaining coverage

The external 29 September 2026 workbook/report records 89 scenarios: 72 Pass, five
Fail and 12 Blocked. Adding these files does not close those findings or change those
historical results. The original Selenium suite also has selector/timing maintenance
and a registration-number edit expectation to review.

The controlled Fleet unavailable/timeout/malformed-response HTTP tests, real Fleet
to Trip eligibility lookup, remote CI execution and isolated successful role-assignment
unit test are not implemented by these additions. The current fake-Fleet unit check
proves only that a rejected status update does not partially assign a trip; it does
not prove API error handling or complete recovery after a Trip database failure.

Keep the Word report, Excel workbook and raw run evidence in the external QA folder.
No application service code, database schema or deployment configuration is changed
by these QA additions.

## Validation of these additions on 29 September 2026

- Registration validation: seven passing checks.
- Dispatch workflow: nine passing checks; the two existing lifecycle checks also passed.
- Browser recheck: 23 passing scenarios and the one known rejection expectation failure.
  The overall xUnit workflow correctly returned Fail. An initial stale-row timing
  error was corrected by reacquiring elements during explicit waits.
- JMeter smoke recheck: one thread and one loop, eight workflow samples and zero errors.
  This confirms the plan can execute; it is not a load or NFR acceptance result.

Local validation evidence is under ignored TestResults/QA-Sprint03/repository-validation.
The first JMeter smoke attempt used incorrectly split PowerShell property arguments;
its fixture-exhaustion failures are harness configuration errors, not product findings.
The corrected run and commands use quoted properties and fresh fixtures.
