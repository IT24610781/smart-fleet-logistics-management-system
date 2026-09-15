using FleetVehicleService.Models;

namespace FleetVehicleService.Services;

// ============================================================
// US-06: Publishes vehicle status change events to Kafka
// ============================================================

public interface IVehicleEventPublisher
{
    Task PublishStatusChangedAsync(
        Vehicle vehicle, string previousStatus, string newStatus);
}