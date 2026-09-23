namespace TripDispatchService.Models;

// The five lifecycle states. They must match the CHECK constraint on
// trips.status in database/trip-dispatch/schema.sql.
//
//   Requested -> Assigned -> In-Progress -> Completed      (or Cancelled)
public static class TripStatus
{
    public const string Requested = "Requested";
    public const string Assigned = "Assigned";
    public const string InProgress = "In-Progress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Requested, Assigned, InProgress, Completed, Cancelled
    };
}