namespace TripDispatchService.DTOs;

// SCRUM-147/148: Driver Performance Report (US-19).
public class DriverPerformanceResponse
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<DriverPerformanceEntry> Drivers { get; set; } = [];
}

public class DriverPerformanceEntry
{
    public Guid DriverId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Deliberately no combined score or ranking (DoD: "No unsupported score
    // or ranking is presented") - raw, individually-traceable counts only.
    public int AcceptedCount { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }

    // False when none of the three counts are non-zero - AC Scenario 3.
    public bool HasActivity { get; set; }
}