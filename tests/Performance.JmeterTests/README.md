# Performance Testing (JMeter)

**SE3112 Advanced Software Engineering - Take-Home Assessment**
**Individual role: Load & Performance Testing** - Ranjula Adikari (IT24104259)

This folder holds the project's Apache JMeter 5.6.3 test plans, covering all four
microservices. Four plans (`AuthPerformanceTest.jmx`, `FleetVehiclePerformanceTest.jmx`,
`TripDispatchPerformanceTest.jmx`, `TrackingPerformanceTest.jmx`) were originally built
for the SE3022 CSP module's QA cycle and have real, reproducible results documented in
the project's final evaluation report. `FleetVehicleStressTest.jmx` is new for this
SE3112 assessment: it is the one used for the live demo and CI integration, built to
specifically exercise the brief's "under stress or high concurrency" requirement and to
run unattended (no manual token pasting) so it is reliable to execute live or in a
pipeline.

## Tool setup

1. Install JMeter 5.6.3+ and ensure Java 17+ is on PATH.
2. Have the Auth (`localhost:5100`) and Fleet (`localhost:5041`) services running
   locally with their databases migrated (see the repo root README for
   `docker-compose up` / per-service `dotnet run` instructions).
3. Know your local admin login (username defaults to `admin`; pass the real password
   via the `QA_ADMIN_PASSWORD` environment variable at launch - never hardcoded in the
   `.jmx` files). It is read as an environment variable rather than a `-J` command-line
   property because `jmeter.bat`'s own batch-file argument parsing silently mangles `!`
   in `-J` values on Windows (a known delayed-expansion gotcha), which turns a password
   like `P@ssw0rd1!` into an empty string with no warning.

## What `FleetVehicleStressTest.jmx` demonstrates

Two thread groups against `FleetVehicleService` (`POST /api/vehicles`,
`GET /api/vehicles`, `PUT /api/vehicles/{id}`), both asserting the project's NFR target
(under 2 seconds per request) and HTTP status codes:

| Thread group | Shape | Purpose |
|---|---|---|
| 1. Baseline Load | 10 threads, 5s ramp-up, 5 loops (150 requests) | Steady load, same shape as the original SE3022 `FleetVehiclePerformanceTest.jmx`, for a like-for-like comparison point |
| 2. Stress Spike | 60 threads, 2s ramp-up, 3 loops (360 requests) | Near-instant concurrency burst intended to surface contention (DB connection pool, thread pool exhaustion) that a gentle steady load won't - this is the profile that targets "high concurrency" specifically |

A `JSR223PreProcessor` ("Login as Admin") logs in once per thread and stores the JWT in
`authToken`, which the shared `HTTP Header Manager` injects as `Bearer ${authToken}` on
every request - no manual token editing before a run, which matters for live-demo
reliability.

## Running it

**Live demo / CLI (non-GUI, generates an HTML dashboard):**

```
cd tests/performance
run-fleet-stress-demo.bat <your_admin_password>
```

(The script sets `QA_ADMIN_PASSWORD` for you from the argument - see the note on `!`
above for why this isn't passed as a `-J` property.)

This runs headless (`jmeter -n -t ... -l results.jtl -e -o dashboard/`), which is more
reliable under time pressure than GUI mode, and opens a browsable HTML report
(`dashboard/index.html`) showing requests/sec, response-time percentiles (median, 90th,
95th, 99th), error rate, and a response-time-over-time graph for both thread groups.

**Manual / GUI mode** (for exploring live, not for the timed demo):

```
jmeter -t FleetVehicleStressTest.jmx -Jqa.adminPassword=<your_admin_password>
```

Then View Results Tree / Summary Report / Aggregate Report listeners work as usual.

**Other three plans** (already-documented SE3022 results; same pattern applies):

```
jmeter -n -t AuthPerformanceTest.jmx -l results/auth.jtl -e -o results/auth-dashboard
jmeter -n -t FleetVehiclePerformanceTest.jmx -Jqa.adminPassword=<admin_password> -l results/fleet.jtl -e -o results/fleet-dashboard
jmeter -n -t TripDispatchPerformanceTest.jmx -Jqa.fixtures=trip-fixtures.json -l results/trip.jtl -e -o results/trip-dashboard
jmeter -n -t TrackingPerformanceTest.jmx -Jqa.fixtures=tracking-fixtures.json -l results/tracking.jtl -e -o results/tracking-dashboard
```

(Trip Dispatch and Tracking need fresh fixtures first - see `prepare_trip_fixtures.py`
and `prepare_tracking_fixtures.py` in this folder; records accumulate in the databases
on each run, so stale fixtures will fail.)

## Pipeline integration

The `.github/workflows/build.yml` CI pipeline currently runs `dotnet test` per service
and a frontend build/lint; it does not yet run JMeter (load tests are long-running and
depend on seeded runtime data, so they are deliberately kept out of the fast
build-on-every-push gate, consistent with how most teams scope performance testing to a
separate, scheduled or on-demand job rather than every PR). For the SE3112 group
automation demo, `FleetVehicleStressTest.jmx` can be invoked the same way as above from
any shell step or a dedicated workflow job; it requires `qa.adminPassword` and reachable
Auth/Fleet services (e.g. the `mysql` service container already defined in `build.yml`,
plus the two services started via `dotnet run` or Docker in that job).

## Results summary (real, measured - see the final SE3022 report for full detail)

| Plan | NFR target | Real result |
|---|---|---|
| AuthPerformanceTest.jmx | Under 2s | 100 samples, 10% error (Register Dispatcher 20%, Login 0%) |
| FleetVehiclePerformanceTest.jmx | Under 2s | 150 samples, 1.33% error |
| TripDispatchPerformanceTest.jmx | Under 2s | 400 samples, 0.00% error, but 2 of 8 groups exceed the target badly (p95 9-10.8s) |
| TrackingPerformanceTest.jmx | p95 under 2s (report under 5s), error rate under 1% | 916 samples, 4.04% error |
| FleetVehicleStressTest.jmx (new) | Under 2s | 510 samples, 6.47% error overall (run against the Dockerized stack, `docker-compose up`). Baseline: Create 6.00% (50 samples), List 0.00%, Update 0.00%. Stress Spike: Create 16.67% (180 samples), List 0.00% - Create's error rate nearly triples under the spike, and its median response time jumps from 74.5ms (baseline) to 519ms (spike), a 7x increase, with p95 climbing from 2992ms to 4600ms |

## Design rationale and key findings

**Load vs. stress vs. spike.** Load testing checks behaviour under expected
concurrency. The Stress Spike thread group intentionally exceeds normal concurrency
almost instantaneously instead of ramping gradually, so it surfaces degradation that a
steady load would not.

**Assertions, not just timers.** Each sampler carries a `DurationAssertion` (under
2000ms) and a `ResponseAssertion` (expected status code), so JMeter reports a sample as
failed if either condition fails, even on an HTTP 200. This turns "the request
completed" into "the request met the NFR."

**Percentiles over averages.** An average can hide a long tail; p95/p99 (visible in the
Aggregate Report and the HTML dashboard) show what the slowest 5%/1% of requests
actually looked like, which is what the project's "under 2 seconds" NFR is meant to
protect against.

**What the stress group found that the baseline didn't.** Concurrency problems (DB
connection pool exhaustion, lock contention) often don't appear until enough requests
arrive close together - a slow ramp gives the system time to process requests without
ever having many in flight at once. In the measured run, Create Vehicle's error rate
nearly tripled (6.00% to 16.67%) and its median response time rose 7x (74.5ms to 519ms)
under the spike, while List and Update were unaffected - evidence the failure is
concurrency-related rather than a general capacity limit.

**Prior finding from the same style of testing.** `TripDispatchPerformanceTest.jmx`
measured p95 latency of 9-10.8 seconds on 2 of 8 workflow groups under load, despite 0%
errors - a performance issue that functional testing alone would not catch, since every
individual request still succeeded.

**A schema-drift finding from building this test.** The original SE3022
`FleetVehiclePerformanceTest.jmx` sends a `PUT /api/vehicles/{id}` body without
`registrationNo` and reported only 1.33% error. Replaying the same body against the
current API returned `400 Bad Request` on every call - the service's validation now
requires `registrationNo`, `type`, `capacity`, and `status` together on update (confirmed
by testing the endpoint directly with `Invoke-RestMethod`), which was evidently not the
case when the original test was written. `FleetVehicleStressTest.jmx` extracts
`registrationNo` from the Create response and includes it on Update to match current
behaviour. This illustrates why a performance test suite needs to be re-validated against
the live API rather than re-run unchanged from an old recording - a stale request body
can silently present as "100% failure," which looks like a performance problem but is
actually a contract mismatch.
