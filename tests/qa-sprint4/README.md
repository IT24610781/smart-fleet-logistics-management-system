# Sprint 4 QA automation

Scope: US-14 live location updates, US-15 live fleet map, US-16 location history, and
(as they merge) US-17 to US-19 reports. These additions extend the Sprint 2 and Sprint 3
backend, Selenium and JMeter tests.

## Included code

| File | Coverage |
| --- | --- |
| `../Frontend.SeleniumTests/TrackingPageTests.cs` | SCRUM-154: six live browser tests for `/dispatcher/tracking` (US-15) |
| `../performance/TrackingPerformanceTest.jmx` | SCRUM-155: writes, map reads, history reads, utilization report, mixed workload |
| `../performance/prepare_tracking_fixtures.py` | Fresh Drivers, Vehicles and In-Progress trips for each performance run |

## 1 Start the live environment

```powershell
docker-compose up -d
```

Create `frontend/.env.local` (git-ignored) so the browser talks to the local services, not Azure:

```
VITE_API_BASE_URL=http://localhost:5100
VITE_VEHICLE_API_URL=http://localhost:5041
VITE_TRIP_API_URL=http://localhost:5051
VITE_TRACKING_API_URL=http://localhost:5000
```

Then start the frontend in a separate terminal:

```powershell
cd frontend
npm run dev
```

## 2 Run the Selenium tests (SCRUM-154)

Chrome must be installed. The tests create their own accounts, vehicles and trips, so use a
test environment, and do not run browser and load tests at the same time.

| Environment variable | Default or purpose |
| --- | --- |
| `QA_RUN_SPRINT3_UI` | Set to `1` to enable live browser tests |
| `QA_ADMIN_PASSWORD` | Required |
| `QA_ADMIN_USERNAME` | `admin` |
| `QA_FRONTEND_URL` | `http://localhost:5173` |
| `QA_AUTH_URL` / `QA_FLEET_URL` / `QA_TRIP_URL` / `QA_TRACKING_URL` | `5100` / `5041` / `5051` / `5000` on localhost |
| `QA_SELENIUM_HEADLESS` | Set to `0` to watch the browser |

```powershell
$env:QA_RUN_SPRINT3_UI="1"; $env:QA_ADMIN_PASSWORD="<admin password>"
dotnet test tests/Frontend.SeleniumTests/Frontend.SeleniumTests.csproj --filter "Category=Sprint4Live" --logger "trx;LogFileName=US15-selenium.trx" --results-directory "TestResults/QA-Sprint04/selenium"
```

Screenshots are written to `TestResults/QA-Sprint04/ui-screenshots`.

## 3 Run the performance test (SCRUM-155)

Install Java and Apache JMeter 5.6.3, and make sure `jmeter` is on PATH. Run from the repository root.

```powershell
$env:QA_ADMIN_PASSWORD="<admin password>"
python tests/performance/prepare_tracking_fixtures.py --output tests/performance/perf-fixtures-tracking.json --drivers 20

jmeter -n -t tests/performance/TrackingPerformanceTest.jmx `
  -Jqa.fixtures=tests/performance/perf-fixtures-tracking.json `
  -Jqa.adminPassword=$env:QA_ADMIN_PASSWORD `
  -Jqa.threads=20 `
  -l tests/performance/results/tracking.jtl -e -o tests/performance/results/tracking-html
```

- Keep `--drivers` at least as large as `qa.threads`.
- The HTML output folder must be empty or not exist.
- Generate fresh fixtures for every recorded run. Location history grows with each run.
- Targets: p95 under 2 s (report under 5 s) and error rate under 1 percent.
- The page polls every 15 s. Use `-Jqa.mapThinkMs=15000` for the realistic dispatcher pattern.
  The default of 1000 ms is a harsher test.

Suggested runs: baseline (`--drivers 10`), load (`--drivers 50 -Jqa.threads=50`),
stress (`--drivers 100 -Jqa.threads=100`).

## Manual checks

- Empty state ("No trips in progress"): only valid on a clean database (`docker-compose down -v`).
- Tracking service down: `docker-compose stop tracking-service`, reload the page, confirm the error
  message, then `docker-compose start tracking-service`.
