namespace TripDispatchService.Services;

// SCRUM-148 (US-19): just enough of a Driver account for the performance
// report - reuses the existing api/users/drivers endpoint on AuthUserService
// (same one DriverEligibilityClient already calls).
public record DriverSummary(Guid Id, string FirstName, string LastName, bool IsActive);

public interface IDriverDirectoryClient
{
    Task<List<DriverSummary>> GetDriversAsync(string? authorizationHeader, CancellationToken cancellationToken);
}