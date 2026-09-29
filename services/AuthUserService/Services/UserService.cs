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

        return await SaveNewUserAsync(request, "Driver");
    }


    // ============================================================
    // US-08: Dispatcher creates their own account
    //
    // Unlike CreateUserAsync (US-01), this collects ALL validation
    // problems and reports them per field, because AC Scenario 2
    // asks for "clear field-level validation errors".
    // The role is decided here on the server - the request DTO has no
    // Role property, so a caller can never register as Admin.
    // ============================================================

    public async Task<UserResponse> RegisterDispatcherAsync(
        CreateUserRequest request)
    {
        var errors = ValidateRegistration(request);

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return await SaveNewUserAsync(request, "Dispatcher");
    }

    // Limits mirror database/auth-user/schema.sql (SCRUM-86 lesson:
    // validate against the column size BEFORE the INSERT, otherwise MySQL
    // throws "Data too long" and the API returns a raw 500).
    private const int UsernameMaxLength = 50;
    private const int EmailMaxLength = 255;
    private const int NameMaxLength = 100;
    private const int ContactNumberMaxLength = 20;

    // Same rules as the frontend (src/utils/validation.js) so the UI and
    // the API always agree on what is valid.
    private static readonly System.Text.RegularExpressions.Regex UsernamePattern =
        new(@"^[a-zA-Z][a-zA-Z0-9_.]{2,19}$");

    private static readonly System.Text.RegularExpressions.Regex EmailPattern =
        new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    private static Dictionary<string, string> ValidateRegistration(
        CreateUserRequest request)
    {
        var errors = new Dictionary<string, string>();

        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var contactNumber = request.ContactNumber?.Trim() ?? string.Empty;

        if (username.Length == 0)
            errors["username"] = "Username is required.";
        else if (username.Length > UsernameMaxLength)
            errors["username"] =
                $"Username must be {UsernameMaxLength} characters or fewer.";
        else if (!UsernamePattern.IsMatch(username))
            errors["username"] =
                "Username must be 3-20 characters, start with a letter, and use only letters, numbers, \"_\" or \".\".";

        if (email.Length == 0)
            errors["email"] = "Email is required.";
        else if (email.Length > EmailMaxLength)
            errors["email"] =
                $"Email must be {EmailMaxLength} characters or fewer.";
        else if (!EmailPattern.IsMatch(email))
            errors["email"] = "Enter a valid email address.";

        if (password.Length == 0)
            errors["password"] = "Password is required.";
        else if (password.Length < 8)
            errors["password"] = "Password must be at least 8 characters.";

        if (firstName.Length == 0)
            errors["firstName"] = "First name is required.";
        else if (firstName.Length > NameMaxLength)
            errors["firstName"] =
                $"First name must be {NameMaxLength} characters or fewer.";

        if (lastName.Length == 0)
            errors["lastName"] = "Last name is required.";
        else if (lastName.Length > NameMaxLength)
            errors["lastName"] =
                $"Last name must be {NameMaxLength} characters or fewer.";

        if (contactNumber.Length == 0)
        {
            errors["contactNumber"] = "Contact number is required.";
        }
        else
        {
            var digitCount = contactNumber.Count(char.IsDigit);

            if (contactNumber.Length > ContactNumberMaxLength ||
                digitCount < 7 || digitCount > 15)
            {
                errors["contactNumber"] = "Enter a valid contact number.";
            }
        }

        return errors;
    }


    // ============================================================
    // Shared by US-01 (Driver) and US-08 (Dispatcher):
    // normalise -> reject duplicates -> hash password -> save.
    // Callers MUST have validated the request already.
    // ============================================================

    private async Task<UserResponse> SaveNewUserAsync(
        CreateUserRequest request, string role)
    {
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

        // Create the user with the role chosen by the server
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

            Role = role,

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

        if (!string.IsNullOrEmpty(request.Password))
            throw new ArgumentException("Use the verified Change password flow to update your password.");

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