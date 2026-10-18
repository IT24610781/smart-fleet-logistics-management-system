using FleetVehicleService.Data;
using FleetVehicleService.Models;
using FleetVehicleService.Services;

namespace FleetVehicleService.Tests;

[Trait("Category", "Unit")]
public class VehicleServiceUnitTests
{
    [Theory]
    [InlineData(VehicleStatus.Available, VehicleStatus.OnTrip)]
    [InlineData(VehicleStatus.OnTrip, VehicleStatus.Available)]
    public async Task ValidTransition_SavesBeforePublishingExactlyOneEvent(string initial, string target)
    {
        var calls = new List<string>();
        var repository = new RecordingRepository(NewVehicle(initial), calls);
        var events = new RecordingPublisher(calls);
        var service = new VehicleService(repository, events, new StubEligibility());

        var result = await service.UpdateVehicleStatusAsync(repository.Vehicle!.Id, target);

        Assert.Equal(target, result.Status);
        Assert.Equal(1, repository.Updates);
        Assert.Equal(new[] { "save", "publish" }, calls);
        Assert.Equal((result.Id, initial, target), Assert.Single(events.Events));
    }

    [Theory]
    [InlineData(VehicleStatus.Available, VehicleStatus.Available)]
    [InlineData(VehicleStatus.OnTrip, VehicleStatus.OnTrip)]
    [InlineData(VehicleStatus.UnderMaintenance, VehicleStatus.OnTrip)]
    [InlineData(VehicleStatus.UnderMaintenance, VehicleStatus.Available)]
    public async Task InvalidTransition_DoesNotWriteOrPublish(string initial, string target)
    {
        var calls = new List<string>();
        var repository = new RecordingRepository(NewVehicle(initial), calls);
        var events = new RecordingPublisher(calls);
        var service = new VehicleService(repository, events, new StubEligibility());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateVehicleStatusAsync(repository.Vehicle!.Id, target));

        Assert.Equal(initial, repository.Vehicle!.Status);
        Assert.Equal(0, repository.Updates);
        Assert.Empty(events.Events);
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Flying")]
    public async Task InvalidStatus_ReportsValidationWithoutSideEffects(string? target)
    {
        var calls = new List<string>();
        var repository = new RecordingRepository(NewVehicle(VehicleStatus.Available), calls);
        var events = new RecordingPublisher(calls);
        var service = new VehicleService(repository, events, new StubEligibility());

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateVehicleStatusAsync(repository.Vehicle!.Id, target));

        Assert.Contains("status", error.Errors.Keys);
        Assert.Empty(calls);
        Assert.Equal(VehicleStatus.Available, repository.Vehicle!.Status);
    }

    [Fact]
    public async Task SaveFailure_DoesNotPublishSuccessfulStatusChange()
    {
        var calls = new List<string>();
        var failure = new IOException("Simulated persistence failure");
        var repository = new RecordingRepository(NewVehicle(VehicleStatus.Available), calls)
        {
            UpdateFailure = failure
        };
        var events = new RecordingPublisher(calls);
        var service = new VehicleService(repository, events, new StubEligibility());

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            service.UpdateVehicleStatusAsync(repository.Vehicle!.Id, VehicleStatus.OnTrip));

        Assert.Same(failure, actual);
        Assert.Equal(new[] { "save" }, calls);
        Assert.Equal(0, repository.Updates);
        Assert.Empty(events.Events);
    }

    [Theory]
    [InlineData(VehicleStatus.Available, false, true, 1)]
    [InlineData(VehicleStatus.UnderMaintenance, false, true, 1)]
    [InlineData(VehicleStatus.Available, true, false, 1)]
    [InlineData(VehicleStatus.UnderMaintenance, true, false, 1)]
    [InlineData(VehicleStatus.OnTrip, false, false, 0)]
    [InlineData(VehicleStatus.OnTrip, true, false, 0)]
    public async Task Delete_EnforcesVehicleAndTripRules(
        string status, bool activeTrips, bool allowed, int expectedEligibilityCalls)
    {
        var calls = new List<string>();
        var repository = new RecordingRepository(NewVehicle(status), calls);
        var eligibility = new StubEligibility { HasTrips = activeTrips };
        var events = new RecordingPublisher(calls);
        var service = new VehicleService(repository, events, eligibility);
        var id = repository.Vehicle!.Id;

        if (allowed) await service.DeleteVehicleAsync(id);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteVehicleAsync(id));

        Assert.Equal(allowed ? 1 : 0, repository.DeletedIds.Count);
        if (allowed) Assert.Equal(id, Assert.Single(repository.DeletedIds));
        Assert.Equal(expectedEligibilityCalls, eligibility.Calls);
        if (expectedEligibilityCalls > 0) Assert.Equal(id, eligibility.LastId);
        Assert.Empty(events.Events);
    }

    [Fact]
    public async Task Delete_EligibilityUnavailable_DoesNotDelete()
    {
        var repository = new RecordingRepository(NewVehicle(VehicleStatus.Available), []);
        var failure = new HttpRequestException("Trip service unavailable");
        var eligibility = new StubEligibility { Failure = failure };
        var service = new VehicleService(repository, new RecordingPublisher([]), eligibility);

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.DeleteVehicleAsync(repository.Vehicle!.Id));

        Assert.Same(failure, actual);
        Assert.Equal(1, eligibility.Calls);
        Assert.Empty(repository.DeletedIds);
    }

    [Fact]
    public async Task Delete_MissingVehicle_DoesNotConsultTripOrDelete()
    {
        var repository = new RecordingRepository(null, []);
        var eligibility = new StubEligibility();
        var service = new VehicleService(repository, new RecordingPublisher([]), eligibility);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteVehicleAsync(Guid.NewGuid()));

        Assert.Equal(0, eligibility.Calls);
        Assert.Empty(repository.DeletedIds);
    }

    private static Vehicle NewVehicle(string status) => new()
    {
        Id = Guid.NewGuid(), RegistrationNo = "UNIT-001", Type = "Van", Capacity = "8 persons", Status = status
    };

    // Only operations used by these tests are implemented; unexpected calls fail loudly.
    private sealed class RecordingRepository(Vehicle? vehicle, List<string> calls) : IVehicleRepository
    {
        public Vehicle? Vehicle { get; } = vehicle;
        public Exception? UpdateFailure { get; init; }
        public int Updates { get; private set; }
        public List<Guid> DeletedIds { get; } = [];
        public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(Vehicle?.Id == id ? Vehicle : null);
        public Task UpdateVehicleAsync(Vehicle value)
        {
            calls.Add("save");
            if (UpdateFailure is not null) throw UpdateFailure;
            Updates++;
            return Task.CompletedTask;
        }
        public Task DeleteVehicleAsync(Guid id) { DeletedIds.Add(id); return Task.CompletedTask; }
        public Task CreateVehicleAsync(Vehicle value) => throw new NotSupportedException();
        public Task<Vehicle?> GetByRegistrationNoAsync(string value) => throw new NotSupportedException();
        public Task<List<Vehicle>> GetAllAsync() => throw new NotSupportedException();
    }

    private sealed class RecordingPublisher(List<string> calls) : IVehicleEventPublisher
    {
        public List<(Guid Id, string Previous, string Current)> Events { get; } = [];
        public Task PublishStatusChangedAsync(Vehicle vehicle, string previousStatus, string newStatus)
        {
            calls.Add("publish");
            Events.Add((vehicle.Id, previousStatus, newStatus));
            return Task.CompletedTask;
        }
    }

    private sealed class StubEligibility : ITripEligibilityChecker
    {
        public bool HasTrips { get; init; }
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }
        public Guid LastId { get; private set; }
        public Task<bool> HasActiveOrUpcomingTripsAsync(Guid vehicleId)
        {
            Calls++;
            LastId = vehicleId;
            if (Failure is not null) throw Failure;
            return Task.FromResult(HasTrips);
        }
    }
}
