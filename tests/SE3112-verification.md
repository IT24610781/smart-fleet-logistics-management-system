# SE3112 verification record

Verified on 18 October 2026, from dev baseline `0670df8` on branch
`feature/se3112-unit-mocking`.

## Isolated unit runner

Command: `./scripts/test-unit-mocking.ps1`

| Project | Selected cases | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Auth | 12 | 12 | 0 | 0 |
| Fleet | 19 | 19 | 0 | 0 |
| Trip | 22 | 22 | 0 | 0 |
| Tracking | 79 | 79 | 0 | 0 |
| Total | 132 | 132 | 0 | 0 |

48 cases were added: Auth 12, Fleet 19, Trip 13, Tracking 4.
84 selected cases were already part of the team project and are now classified for
this runner. This is selected-suite coverage, not a percentage of production code coverage.

The `-NoBuild` demo run passed after removal of the disposable MySQL container and
completed in approximately 10 seconds locally. First-time restore/build takes longer.
Local evidence folders (ignored by Git):

- `TestResults/unit-mocking/20261018-155726-9fcd6132/`
- `TestResults/unit-mocking/20261018-160054-4111edc4/`

## Broader regression verification

All four API projects ran with a disposable local `mysql:8.0` container and isolated
Auth/Fleet test schemas. Trip's guard tests used the same disposable server and their
own temporary schemas. The container and its anonymous volume were removed afterward.

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Auth | 105 | 0 | 0 |
| Fleet | 105 | 0 | 0 |
| Trip | 80 | 0 | 0 |
| Tracking | 102 | 0 | 18 |
| Total | 392 | 0 | 18 |

The 18 skips are explicitly opt-in live API tests. They are not counted as passes.
Selenium, performance testing and live Azure workflows were not run as part of this change.
Detailed local TRX files: `TestResults/se3112-integration/`.

`dotnet build SmartFleet.slnx --no-restore --verbosity quiet` passed with zero warnings/errors.
After adding the Integration trait, selecting Trip's database tests with no database
configuration reported four skips and zero failures, verifying discovery is safe.
The skip bug identified in the older checkout was already fixed in dev.

## CI evidence

The workflow publishes `unit-mocking-results` and a four-service job summary. The
GitHub Actions run attached to the pull request is the source for remote execution
status; local results above must not be represented as a remote CI run.
