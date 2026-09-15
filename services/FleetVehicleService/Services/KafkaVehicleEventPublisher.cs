using System.Text.Json;
using Confluent.Kafka;
using FleetVehicleService.Models;

namespace FleetVehicleService.Services;

// ============================================================
// US-06: Publishes a JSON message to the "vehicle.status.events"
// ============================================================

public class KafkaVehicleEventPublisher : IVehicleEventPublisher, IDisposable
{
    private const string Topic = "vehicle.status.events";

    private readonly IProducer<Null, string> _producer;

    public KafkaVehicleEventPublisher(IConfiguration configuration)
    {
        var bootstrapServers =
            configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException(
                    "Kafka bootstrap servers are not configured.");

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task PublishStatusChangedAsync(
        Vehicle vehicle, string previousStatus, string newStatus)
    {
        var payload = new
        {
            vehicleId = vehicle.Id,
            registrationNo = vehicle.RegistrationNo,
            previousStatus,
            newStatus,
            changedAt = DateTime.UtcNow
        };

        var message = new Message<Null, string>
        {
            Value = JsonSerializer.Serialize(payload)
        };

        await _producer.ProduceAsync(Topic, message);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}