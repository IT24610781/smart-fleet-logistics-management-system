using TrackingTelemetryService.Data;
using TrackingTelemetryService.DTOs;
using TrackingTelemetryService.Models;

namespace TrackingTelemetryService.Services;

public class TrackingService(ITrackingRepository repository, ITripDispatchClient tripClient)
{
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxPastAge = TimeSpan.FromHours(24);

    public async Task<LocationUpdateResponse> SubmitLocationAsync(
        Guid driverId,
        SubmitLocationRequest request,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();

        if (request.TripId is null)
        {
            errors["tripId"] = "Trip id is required.";
        }

        if (request.Latitude is null)
        {
            errors["latitude"] = "Latitude is required.";
        }
        else if (request.Latitude is < -90 or > 90)
        {
            errors["latitude"] = "Latitude must be between -90 and 90.";
        }

        if (request.Longitude is null)
        {
            errors["longitude"] = "Longitude is required.";
        }
        else if (request.Longitude is < -180 or > 180)
        {
            errors["longitude"] = "Longitude must be between -180 and 180.";
        }

        if (request.ObservedAt is null)
        {
            errors["observedAt"] = "Observation time is required.";
        }
        else
        {
            var now = DateTime.UtcNow;
            if (request.ObservedAt > now.Add(MaxFutureSkew))
            {
                errors["observedAt"] = "Observation time cannot be in the future.";
            }
            else if (request.ObservedAt < now.Subtract(MaxPastAge))
            {
                errors["observedAt"] = "Observation time is too far in the past.";
            }
        }

        // Nothing is saved unless every field is valid.
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var trip = await tripClient.GetTripAsync(request.TripId!.Value, authorizationHeader, cancellationToken);

        // Scenario 3: a trip that isn't this driver's gets the exact same
        // response as a trip that doesn't exist at all - a driver should
        // never be able to tell the difference by probing trip ids.
        if (trip is null || trip.AssignedDriverId != driverId)
        {
            throw new NotFoundException($"No trip found with id '{request.TripId}'.");
        }

        // The "agreed eligible lifecycle state" from the Definition of
        // Done: only an active, In-Progress trip can receive updates.
        if (trip.Status != "In-Progress")
        {
            throw new InvalidOperationException(
                $"Location updates can only be submitted for an In-Progress trip (this trip is '{trip.Status}').");
        }

        if (trip.AssignedVehicleId is null)
        {
            // Should not happen for a genuinely In-Progress trip (a vehicle
            // is always assigned before that status is reached), but guards
            // against inconsistent data rather than crashing outright.
            throw new InvalidOperationException("This trip has no assigned vehicle.");
        }

        var update = new TripLocationUpdate
        {
            Id = Guid.CreateVersion7(),
            TripId = trip.Id,
            DriverId = driverId,
            VehicleId = trip.AssignedVehicleId.Value,
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            ObservedAt = request.ObservedAt!.Value,
            ReceivedAt = DateTime.UtcNow,
        };

        await repository.CreateAsync(update);

        return new LocationUpdateResponse
        {
            Id = update.Id,
            TripId = update.TripId,
            Latitude = update.Latitude,
            Longitude = update.Longitude,
            ObservedAt = update.ObservedAt,
            ReceivedAt = update.ReceivedAt,
        };
    }

    // SCRUM-134: Authorized location-history retrieval for one trip.

    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;

    public async Task<List<LocationUpdateResponse>> GetHistoryAsync(
        Guid tripId,
        Guid? restrictToDriverId,
        int? limit,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? DefaultLimit;

        if (effectiveLimit <= 0 || effectiveLimit > MaxLimit)
        {
            throw new ValidationException(new Dictionary<string, string>
            {
                ["limit"] = $"Limit must be between 1 and {MaxLimit}."
            });
        }

        var trip = await tripClient.GetTripAsync(tripId, authorizationHeader, cancellationToken);

        // Same identical-message pattern as SubmitLocationAsync: a trip
        // that isn't this driver's looks exactly like a trip that doesn't
        // exist, for a Driver caller. Dispatcher/Admin (restrictToDriverId
        // is null) can see any trip.
        if (trip is null || (restrictToDriverId is not null && trip.AssignedDriverId != restrictToDriverId))
        {
            throw new NotFoundException($"No trip found with id '{tripId}'.");
        }

        var records = await repository.GetByTripIdAsync(tripId, effectiveLimit);

        return records.Select(r => new LocationUpdateResponse
        {
            Id = r.Id,
            TripId = r.TripId,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            ObservedAt = r.ObservedAt,
            ReceivedAt = r.ReceivedAt,
        }).ToList();
    }

    // SCRUM-128: Latest known location for every currently In-Progress
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    public async Task<List<FleetLocationResponse>> GetFleetLatestAsync(
        string? authorizationHeader, CancellationToken cancellationToken)
    {
        var trips = await tripClient.GetInProgressTripsAsync(authorizationHeader, cancellationToken);

        if (trips.Count == 0)
        {
            return [];
        }

        var latestByTrip = await repository.GetLatestPerTripAsync(trips.Select(t => t.Id).ToList());
        var now = DateTime.UtcNow;

        return trips.Select(trip =>
        {
            var response = new FleetLocationResponse
            {
                TripId = trip.Id,
                VehicleId = trip.AssignedVehicleId,
                DriverId = trip.AssignedDriverId,
                PickupLocation = trip.PickupLocation,
                DropoffLocation = trip.DropoffLocation,
            };

            if (latestByTrip.TryGetValue(trip.Id, out var location))
            {
                response.Latitude = location.Latitude;
                response.Longitude = location.Longitude;
                response.ObservedAt = location.ObservedAt;
                response.Freshness = (now - location.ObservedAt) > StaleThreshold ? "stale" : "fresh";
            }
            else
            {
                response.Freshness = "unavailable";
            }

            return response;
        }).ToList();
    }
}