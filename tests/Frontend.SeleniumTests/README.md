# UI / E2E Testing with Selenium - SE3112

**Owner:** Jananjana J.W.C (IT24104006) - UI / E2E Testing
**Project under test:** Smart Logistics & Fleet Management System (SE3022, Group 30)

## My Responsibility

| Member | Testing area |
|---|---|
| Jananjana J.W.C (IT24104006) | UI / E2E testing with Selenium |

## What this suite tests

Selenium WebDriver drives a real Chrome browser against the React frontend, which calls the four microservices
(Auth 5100, Fleet 5041, Tracking 5000, Trip Dispatch 5051). Tests are written in C# with xUnit (.NET 10).

| Area | Test classes | Tests |
|---|---|---|
| Authentication and profile | LoginPageTests, LogoutPageTests, ProfilePageTests, CreateDriverPageTests | 18 |
| Vehicles (CRUD) | VehicleCreate/List/Update/DeletePageTests | 15 |
| Trips and dispatch | TripLifecyclePageTests, SprintDispatchWorkflowTests | 2 |
| Tracking and location | TrackingPageTests, Us14DriverLocationUiTests, Us14LocationFailureTests, Us15FleetMapFailureTests | 14 |
| Reports and access control | ReportsPageTests, Us17FleetUtilizationPageTests, Us18TripCompletionPageTests, Us19DriverPerformancePageTests, Us19AccessTests | 33 |
| **Total** | **19 classes** | **82** |

Key design choices: explicit waits (no `Thread.Sleep`), stale-element-safe waits, `PageLoadStrategy.Eager`,
tests run one at a time (`NoParallelization.cs`) so browsers do not overload the machine, and screenshots on
pass/fail for the Sprint 4 tests.

## Tools

Selenium WebDriver, xUnit, C# (.NET 10), Google Chrome with ChromeDriver, Docker Compose (backend), Vite (frontend).

## Setup

1. Install Docker Desktop, .NET 10 SDK, Node.js and Google Chrome.
2. Copy `.env.example` to `.env` in the repo root and set `FLEET_JWT_KEY` to a long random value.
3. Start the backend: `docker compose up -d` then wait about 1 minute until `docker compose ps` shows everything Up.
4. Start the frontend: `cd frontend`, `npm install`, `npm run dev` (http://localhost:5173).

## Environment variables

| Variable | Purpose |
|---|---|
| `QA_ADMIN_PASSWORD` | Password of the seeded `admin` account (required, never commit it) |
| `QA_ADMIN_USERNAME` | Admin username (default `admin`) |
| `QA_RUN_SPRINT3_UI` | Set to `1` to run the live UI tests that need all services |
| `QA_AUTH_URL`, `QA_FLEET_URL`, `QA_TRIP_URL`, `QA_TRACKING_URL`, `QA_FRONTEND_URL` | Optional URL overrides |

## Pipeline commands

Run everything:

    dotnet test tests/Frontend.SeleniumTests --logger "trx;LogFileName=selenium.trx"

Run one test or class:

    dotnet test tests/Frontend.SeleniumTests --filter "FullyQualifiedName~LoginPageTests"

Live demo (one test per sprint, asks for the admin password):

    .\run-selenium-demo.cmd

In a CI pipeline, run the same `dotnet test` command after `docker compose up -d` and `npm run dev`, with
`QA_ADMIN_PASSWORD` provided as a secret and Chrome in headless mode.

## Results

Fill in from the final run:

| Run date | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| | 82 | | | |

## Troubleshooting

- **Fleet or Trip returns 401 ("audience is invalid")**: the containers are in a bad state. Run `docker compose down`,
  then `docker compose up -d`, wait a minute, and restart `npm run dev`. Do not add `-v` (it deletes data).
- **"Set QA_ADMIN_PASSWORD"**: set the variable in the same terminal window you run the tests from.
- **Tests show Skipped**: set `QA_RUN_SPRINT3_UI=1`.
