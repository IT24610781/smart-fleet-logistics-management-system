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


    // ============================================================
    // US-01: Admin creates Driver account
    // ============================================================

    public async Task<UserResponse> CreateUserAsync(
        CreateUserRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException(
                "Username is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException(
                "Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException(
                "Password is required.");

        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ArgumentException(
                "First name is required.");

        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException(
                "Last name is required.");

        if (string.IsNullOrWhiteSpace(request.ContactNumber))
            throw new ArgumentException(
                "Contact number is required.");

        // Validate password
        if (request.Password.Length < 8)
            throw new ArgumentException(
                "Password must be at least 8 characters.");

        // Normalize values
        var username = request.Username.Trim();

        var email =
            request.Email.Trim().ToLowerInvariant();

        // Check username
        var existingUsername =
            await _userRepository.GetByUsernameAsync(username);

        if (existingUsername != null)
            throw new InvalidOperationException(
                "Username already exists.");

        // Check email
        var existingEmail =
            await _userRepository.GetByEmailAsync(email);

        if (existingEmail != null)
            throw new InvalidOperationException(
                "An account with this email already exists.");

        // Create Driver
        var user = new User
        {
            Id = Guid.CreateVersion7(),

            Username = username,

            Email = email,

            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password),

            FirstName =
                request.FirstName.Trim(),

            LastName =
                request.LastName.Trim(),

            ContactNumber =
                request.ContactNumber.Trim(),

            Role = "Driver",

            IsActive = true,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        // Save
        await _userRepository.CreateUserAsync(user);

        return ToUserResponse(user);
    }

    public async Task<List<UserResponse>> GetDriversAsync()
    {
        var users = await _userRepository.GetDriversAsync();
        return users.Select(ToUserResponse).ToList();
    }


    // ============================================================
    // US-04: View own profile
    // ============================================================

    public async Task<UserResponse> GetProfileAsync(Guid userId)
    {
        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException(
                "User profile not found.");

        return ToUserResponse(user);
    }


    // ============================================================
    // US-04: Edit own profile
    // ============================================================

    public async Task<UserResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request)
    {
        // Validate first name
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ArgumentException(
                "First name is required.");

        // Validate last name
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException(
                "Last name is required.");

        // Validate contact number
        if (string.IsNullOrWhiteSpace(request.ContactNumber))
            throw new ArgumentException(
                "Contact number is required.");

        // Validate password only when supplied
        if (!string.IsNullOrWhiteSpace(request.Password) &&
            request.Password.Length < 8)
        {
            throw new ArgumentException(
                "Password must be at least 8 characters.");
        }

        // Find current user
        var user =
            await _userRepository.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException(
                "User profile not found.");

        // Update editable fields
        user.FirstName =
            request.FirstName.Trim();

        user.LastName =
            request.LastName.Trim();

        user.ContactNumber =
            request.ContactNumber.Trim();

        // Update password only if provided
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password);
        }

        // Update timestamp
        user.UpdatedAt = DateTime.UtcNow;

        // Save
        await _userRepository.UpdateProfileAsync(user);

        return ToUserResponse(user);
    }


    // ============================================================
    // Convert User model -> safe response DTO
    // ============================================================

    private static UserResponse ToUserResponse(User user)
    {
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

    public async Task<UserResponse> GetUserByIdAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
            throw new KeyNotFoundException(
                "User not found.");

        return ToUserResponse(user);
    }
}