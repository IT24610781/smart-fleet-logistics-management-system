using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;

namespace TripDispatchService.Services;

// SCRUM-142/143: Trip Completion Report (US-18).

public class TripCompletionService(ITripRepository tripRepository)
{
    public async Task<TripCompletionResponse> GetTripCompletionAsync(
        DateTime? start, DateTime? end, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();

        if (start is null)
        {
            errors["start"] = "Start date is required.";
        }

        if (end is null)
        {
            errors["end"] = "End date is required.";
        }

        if (start is not null && end is not null && end <= start)
        {
            errors["end"] = "End date must be after the start date.";
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var periodStart = start!.Value;
        var periodEnd = end!.Value;

        var trips = await tripRepository.GetByCreatedDateRangeAsync(periodStart, periodEnd);

        var response = new TripCompletionResponse
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            EligibleTripCount = trips.Count,
            RequestedCount = trips.Count(t => t.Status == TripStatus.Requested),
            AssignedCount = trips.Count(t => t.Status == TripStatus.Assigned),
            InProgressCount = trips.Count(t => t.Status == TripStatus.InProgress),
            CompletedCount = trips.Count(t => t.Status == TripStatus.Completed),
            CancelledCount = trips.Count(t => t.Status == TripStatus.Cancelled),
            HasData = trips.Count > 0,
        };

        response.CompletionRatePercent = response.EligibleTripCount > 0
            ? Math.Round((double)response.CompletedCount / response.EligibleTripCount * 100, 2)
            : 0;

        return response;
    }
}