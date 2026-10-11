\# Failure Handling: Vehicle Status Sync on Trip Assignment



\## Context

When a Dispatcher assigns a vehicle to a trip (`POST /api/trips/{id}/assign`),

TripDispatchService must also tell FleetVehicleService to update that

vehicle's status. Since this involves two separate services and two separate

databases, a decision was needed for what happens if the FleetVehicleService

call fails partway through.



\## Decision: Call Fleet first, persist the trip only after it succeeds



`TripService.AssignVehicleAsync` calls

`IFleetVehicleClient.SetVehicleStatusAsync(vehicleId, "On Trip", ...)`

\*\*before\*\* making any change to the trip record. If that call throws

(`NotFoundException` or `InvalidOperationException` — e.g. the vehicle no

longer exists, or is no longer Available), the method exits immediately.

The trip's in-memory object is never modified, and `\_tripRepository.UpdateAsync`

is never reached.



The same pattern is used in `TripService.RespondAsync`'s reject path: the

vehicle is reverted to "Available" via FleetVehicleService first, and only

then is the trip's local state updated and saved.



\## Why this avoids an inconsistent state

Because the trip is only ever persisted \*after\* Fleet confirms the vehicle

update succeeded, there is no scenario where the trip says "Assigned" to a

vehicle that Fleet doesn't actually show as "On Trip" — the two systems

cannot disagree as a result of a failed call, because a failed call means

the trip write never happens at all. No rollback logic is needed, because

nothing was written in the first place.



\## Trade-off, worth knowing

If the Fleet call succeeds but the trip's own database write then fails

(e.g. a dropped connection to `trip\_dispatch\_db` immediately afterward), the

vehicle would be left "On Trip" for a trip that was never actually recorded

as assigned. This is a narrow window and hasn't been observed, but it is the

one case this pattern doesn't fully protect against. If this ever becomes a

real problem, the next step would be a compensating action (calling Fleet

again to revert the vehicle) if the trip write fails — not implemented today,

since the added complexity isn't justified without evidence it's needed.



\## Applies to

\- `TripService.AssignVehicleAsync` (US-11)

\- `TripService.RespondAsync`, reject path (US-12)

