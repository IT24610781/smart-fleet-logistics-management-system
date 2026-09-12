using FleetVehicleService.DTOs;
using FleetVehicleService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetVehicleService.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly VehicleService _vehicleService;

    public VehiclesController(VehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    // =======US-04: Admin registers a vehicle

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateVehicle(
        CreateVehicleRequest request)
    {
        try
        {
            var vehicle =
                await _vehicleService.CreateVehicleAsync(request);

            return CreatedAtAction(
                nameof(CreateVehicle),
                new { id = vehicle.Id },
                vehicle);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }


    // =======US-05: Dispatcher/Admin views all registered vehicles

    [Authorize(Roles = "Admin,Dispatcher")]
    [HttpGet]
    public async Task<IActionResult> GetVehicles()
    {
        var vehicles = await _vehicleService.GetAllVehiclesAsync();

        return Ok(vehicles);
    }
}