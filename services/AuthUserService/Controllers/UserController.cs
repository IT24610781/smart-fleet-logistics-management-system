using AuthUserService.DTOs;
using AuthUserService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthUserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(request);

            return CreatedAtAction(
                nameof(CreateDriver),
                new { id = user.Id },
                user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
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