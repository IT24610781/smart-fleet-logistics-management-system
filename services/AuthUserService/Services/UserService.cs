using AuthUserService.Data;
using AuthUserService.DTOs;
using AuthUserService.Models;

namespace AuthUserService.Services;

public class UserService
{
    private readonly UserRepository _userRepository;

    public UserService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        // 1. Validate required fields
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("Username is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ArgumentException("First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException("Last name is required.");

        if (string.IsNullOrWhiteSpace(request.ContactNumber))
            throw new ArgumentException("Contact number is required.");

        // 2. Validate password length
        if (request.Password.Length < 8)
            throw new ArgumentException(
                "Password must be at least 8 characters.");

        // 3. Normalize values
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        // 4. Check whether username already exists
        var existingUsername =
            await _userRepository.GetByUsernameAsync(username);

        if (existingUsername != null)
            throw new InvalidOperationException(
                "Username already exists.");

        // 5. Check whether email already exists
        var existingEmail =
            await _userRepository.GetByEmailAsync(email);

        if (existingEmail != null)
            throw new InvalidOperationException(
                "An account with this email already exists.");

        // 6. Create Driver user
        var user = new User
        {
            Id = Guid.CreateVersion7(),

            Username = username,

            Email = email,

            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(request.Password),

            FirstName = request.FirstName.Trim(),

            LastName = request.LastName.Trim(),

            ContactNumber = request.ContactNumber.Trim(),

            // US-01 creates Drivers only
            Role = "Driver",

            IsActive = true,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        // 7. Save user to database
        await _userRepository.CreateUserAsync(user);

        // 8. Return safe API response
        return new UserResponse
        {
            Id = user.Id,

            Username = user.Username,

            Email = user.Email,

            FirstName = user.FirstName,

            LastName = user.LastName,

            ContactNumber = user.ContactNumber,

            Role = user.Role,

            IsActive = user.IsActive,

            CreatedAt = user.CreatedAt,

            UpdatedAt = user.UpdatedAt
        };
    }
}