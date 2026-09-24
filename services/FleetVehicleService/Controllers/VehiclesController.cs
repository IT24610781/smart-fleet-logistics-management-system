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

    //US-04 Admin registers a vehicle

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

    //US-06 Admin updates a vehicle's details and status

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVehicle(
        Guid id, UpdateVehicleRequest request)
    {
        try
        {
            var vehicle =
                await _vehicleService.UpdateVehicleAsync(id, request);

            return Ok(vehicle);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
    }

    //US-11 Dispatcher starts/ends a vehicle's use on a trip

    [Authorize(Roles = "Admin,Dispatcher")]
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateVehicleStatus(
        Guid id, UpdateVehicleStatusRequest request)
    {
        try
        {
            var vehicle =
                await _vehicleService.UpdateVehicleStatusAsync(id, request.Status);

            return Ok(vehicle);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new
            {
                message = "Validation failed.",
                errors = ex.Errors
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
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

    //US-05 Dispatcher/Admin views all registered vehicles

    [Authorize(Roles = "Admin,Dispatcher")]
    [HttpGet]
    public async Task<IActionResult> GetVehicles()
    {
        var vehicles = await _vehicleService.GetAllVehiclesAsync();

        return Ok(vehicles);
    }


    //US-07 Admin removes a vehicle from the registry

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVehicle(Guid id)
    {
        try
        {
            await _vehicleService.DeleteVehicleAsync(id);

            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
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
}