namespace TripDispatchService.DTOs;

// SCRUM-142/143: Trip Completion Report (US-18).
public class TripCompletionResponse
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public int EligibleTripCount { get; set; }
    public int RequestedCount { get; set; }
    public int AssignedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public double CompletionRatePercent { get; set; }
    public bool HasData { get; set; }
}