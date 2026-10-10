# SCRUM-137: Fleet Utilization Report — Formula Design (US-17)

**Story:** SCRUM-26 — US-17: Generate Fleet Utilization Report
**Author:** Ranjula Adikari (IT24104259)
**Status:** Agreed and implemented (see SCRUM-138/139/140)

## Problem

"Utilization" for a vehicle needs to mean **time in use**, not trip count and not current status. The system has no `trip_status_history` table — only a single `updated_at` column on `Trip`, which captures the moment of the *last* status change, not every transition. So there is no stored timestamp for "when did this trip actually become In-Progress."

## Key insight

A Driver's app only submits location pings while their trip is **In-Progress** (enforced server-side in `TrackingService.SubmitLocationAsync`). That means the location-ping history already collected for US-14/US-16 is a real, data-backed proxy for a trip's in-use span: the time between a Completed trip's **first** accepted ping and its **last** accepted ping is (approximately) the time the vehicle was actually on that trip.

This avoids inventing a new table or guessing from `Vehicle.Status`, which only reflects the vehicle's state *now* and can't reconstruct history (e.g. a vehicle currently "Under Maintenance" may have been fully active earlier in the reporting period).

## Formula

For a requested period `[periodStart, periodEnd]`:

1. **Eligible vehicles** = every vehicle with `CreatedAt <= periodEnd`, regardless of current status. A vehicle with zero qualifying trips in the period gets a genuine **0%**, not "unavailable" — it was eligible to be used and wasn't.
2. **Candidate trips** = Completed trips assigned to that vehicle, pre-filtered so only trips whose full lifecycle (`CreatedAt` → `UpdatedAt`) could possibly overlap the period are considered (`UpdatedAt >= periodStart && CreatedAt <= periodEnd`). This is a safe optimization: a trip's full lifecycle always encloses its real ping span, so anything outside this filter cannot contribute hours — and it avoids an unnecessary location-history lookup for every trip that's obviously outside the window.
3. **Per trip:** fetch its location pings, take `first = MIN(observed_at)`, `last = MAX(observed_at)`.
   - If fewer than 2 pings exist, the trip has no measurable duration — it contributes 0 hours, and the vehicle is flagged `HasInsufficientData = true` (see AC Scenario 4 below).
   - Otherwise, **clip** the span to the requested period: `clippedStart = max(first, periodStart)`, `clippedEnd = min(last, periodEnd)` (AC Scenario 2 — a trip that started before or ended after the window should only count the portion inside it).
4. **Per vehicle:** `InUseHours` = sum of all its trips' clipped spans. `UtilizationPercent = min(100, InUseHours / PeriodHours * 100)`, rounded to 2dp.
5. **Fleet summary:** `OverallUtilizationPercent = TotalInUseHours / TotalPossibleHours`, where `TotalPossibleHours = EligibleVehicleCount * PeriodHours`. This is **total hours over total hours**, not an average of each vehicle's percentage — averaging percentages would let one idle vehicle in a large fleet drag the summary down disproportionately (or one maxed-out vehicle inflate it), which total-hours-based doesn't.

## Edge cases / AC mapping

| AC Scenario | Handling |
|---|---|
| Invalid period (end before start) | 400 with validation errors. Equal start/end is also rejected — a zero-length window isn't a meaningful period, even though the AC only literally said "before." |
| Period boundary clipping | Ping span is clipped to `[periodStart, periodEnd]` before computing hours. |
| Vehicle with zero completed trips | Real 0%, not flagged as missing/unavailable. |
| Trip with < 2 pings | Contributes 0 hours, but sets `HasInsufficientData = true` on the vehicle — shown in the UI as a "Limited data" badge rather than silently rounding to a falsely-precise 0%. |
| Vehicle registered after the period ends | Excluded from the report entirely (not eligible). |

## Authorization

Admin + Dispatcher — sourced from an existing comment in `Reports.jsx` ("Shared by Admin and Dispatcher — both are authorized per the SRS").

## Known approximation

`GetByTripIdAsync` is capped at 1000 pings per trip (the endpoint's existing `MaxLimit`). This assumes no single trip generates more than 1000 accepted pings, which holds comfortably at the ~15s Driver polling interval for any realistically long trip.

## Where this lives in code

Implemented in `services/TrackingTelemetryService/Services/FleetUtilizationService.cs` (comments there mirror this document), exposed via `GET /api/reports/fleet-utilization` on `TrackingTelemetryService` (consolidated here rather than a separate microservice, since the location-ping data it depends on already lives in this service). Covered by `FleetUtilizationServiceTests.cs` (11 tests, part of the 43/43 passing suite).