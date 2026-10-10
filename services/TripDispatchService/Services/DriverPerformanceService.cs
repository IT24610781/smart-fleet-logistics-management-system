using TripDispatchService.Data;
using TripDispatchService.DTOs;
using TripDispatchService.Models;

namespace TripDispatchService.Services;

// SCRUM-147/148: Driver Performance Report (US-19).
public class DriverPerformanceService(
    IDriverDirectoryClient driverDirectoryClient,
    ITripRepository tripRepository)
{
    public async Task<DriverPerformanceResponse> GetDriverPerformanceAsync(
        DateTime? start, DateTime? end, string? authorizationHeader, CancellationToken cancellationToken)
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

        var drivers = await driverDirectoryClient.GetDriversAsync(authorizationHeader, cancellationToken);
        var trips = await tripRepository.GetByCreatedDateRangeAsync(periodStart, periodEnd);

        var acceptedByDriver = trips
            .Where(t => t.AssignedDriverId is not null &&
                (t.Status == TripStatus.InProgress || t.Status == TripStatus.Completed))
            .GroupBy(t => t.AssignedDriverId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var completedByDriver = trips
            .Where(t => t.AssignedDriverId is not null && t.Status == TripStatus.Completed)
            .GroupBy(t => t.AssignedDriverId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var rejectedByDriver = trips
            .Where(t => t.RejectedByDriverId is not null)
            .GroupBy(t => t.RejectedByDriverId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var entries = drivers.Select(driver =>
        {
            var accepted = acceptedByDriver.GetValueOrDefault(driver.Id);
            var completed = completedByDriver.GetValueOrDefault(driver.Id);
            var rejected = rejectedByDriver.GetValueOrDefault(driver.Id);

            return new DriverPerformanceEntry
            {
                DriverId = driver.Id,
                FirstName = driver.FirstName,
                LastName = driver.LastName,
                IsActive = driver.IsActive,
                AcceptedCount = accepted,
                CompletedCount = completed,
                RejectedCount = rejected,
                HasActivity = accepted > 0 || completed > 0 || rejected > 0,
            };
        }).ToList();

        return new DriverPerformanceResponse
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Drivers = entries,
        };
    }
}