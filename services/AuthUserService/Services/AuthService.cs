using AuthUserService.Data;
using AuthUserService.DTOs;

namespace AuthUserService.Services;

public class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly JwtService _jwtService;

    public AuthService(
        UserRepository userRepository,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        // 1. Validate username
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("Username is required.");

        // 2. Validate password
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.");

        // 3. Normalize username
        var username = request.Username.Trim();

        // 4. Find user
        var user =
            await _userRepository.GetByUsernameAsync(username);

        if (user == null)
            throw new UnauthorizedAccessException(
                "Invalid username or password.");

        // 5. Check whether account is active
        if (!user.IsActive)
            throw new UnauthorizedAccessException(
                "This account is inactive.");

        // 6. Verify password
        var passwordValid =
            BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

        if (!passwordValid)
            throw new UnauthorizedAccessException(
                "Invalid username or password.");

        // 7. Generate JWT
        var tokenResult =
            _jwtService.GenerateToken(user);

        // 8. Return login response
        return new LoginResponse
        {
            Token = tokenResult.Token,

            ExpiresAt = tokenResult.ExpiresAt,

            User = new UserResponse
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
            }
        };
    }
}