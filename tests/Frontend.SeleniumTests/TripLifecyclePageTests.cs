using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenQA.Selenium;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class TripLifecyclePageTests : SeleniumTestBase
{
    private const string AuthBaseUrl = "http://localhost:5100";
    private const string TripBaseUrl = "http://localhost:5051";
    private const string FleetBaseUrl = "http://localhost:5041";

    [Fact]
    public async Task TripLifecycle_StatusUpdate_IsVisible_WithTimestamp()
    {
        // ============================================================
        // Arrange: create a fresh real Driver account
        // ============================================================

        var (driverUsername, driverPassword) =
            await TestDataSeeder.CreateFreshDriverAsync("life1");

        var driverId =
            await GetDriverIdAsync(driverUsername);

        // ============================================================
        // Arrange: create role-valid JWTs
        // ============================================================

        var dispatcherToken =
            MintRoleToken(
                role: "Dispatcher",
                subject: Guid.NewGuid());

        var driverToken =
            MintRoleToken(
                role: "Driver",
                subject: driverId);

        
        var adminToken = await TestDataSeeder.GetAdminTokenAsync();

        // ============================================================
        // Arrange: create a real test vehicle
        // ============================================================

        await CreateTestVehicleAsync(adminToken);

        // ============================================================
        // Arrange: create a real trip
        // ============================================================

        var uniquePickup =
            $"Selenium US13 Pickup {Guid.NewGuid():N}";

        var uniqueDropoff =
            $"Selenium US13 Dropoff {Guid.NewGuid():N}";

        using var tripClient =
            CreateAuthenticatedClient(dispatcherToken);

        var createResponse =
            await tripClient.PostAsJsonAsync(
                "/api/trips",
                new
                {
                    pickupLocation = uniquePickup,
                    pickupLat = (decimal?)null,
                    pickupLng = (decimal?)null,

                    dropoffLocation = uniqueDropoff,
                    dropoffLat = (decimal?)null,
                    dropoffLng = (decimal?)null,

                    requiredCapacity = "6 persons"
                });

        var createBody =
            await createResponse.Content.ReadAsStringAsync();

        Assert.True(
            createResponse.IsSuccessStatusCode,
            $"Trip creation failed. " +
            $"Status: {(int)createResponse.StatusCode} " +
            $"{createResponse.StatusCode}. " +
            $"Body: {createBody}");

        var trip =
            await createResponse.Content
                .ReadFromJsonAsync<TripDto>()
            ?? throw new InvalidOperationException(
                "Trip creation returned no trip data.");

        Assert.Equal(
            "Requested",
            trip.Status);

        // ============================================================
        // Arrange: get an eligible vehicle suggestion
        // ============================================================

        var suggestionsResponse =
            await tripClient.GetAsync(
                $"/api/trips/{trip.Id}/suggestions");

        var suggestionsBody =
            await suggestionsResponse.Content.ReadAsStringAsync();

        Assert.True(
            suggestionsResponse.IsSuccessStatusCode,
            $"Vehicle suggestion request failed. " +
            $"Status: {(int)suggestionsResponse.StatusCode} " +
            $"{suggestionsResponse.StatusCode}. " +
            $"Body: {suggestionsBody}");

        var suggestions =
            JsonSerializer.Deserialize<
                List<VehicleSuggestionDto>>(
                suggestionsBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? new List<VehicleSuggestionDto>();

        Assert.NotEmpty(
            suggestions);

        var vehicle =
            suggestions.First();

        Assert.NotEqual(
            Guid.Empty,
            vehicle.VehicleId);

        // ============================================================
        // Arrange: assign Driver + Vehicle
        // ============================================================

        var assignResponse =
            await tripClient.PostAsJsonAsync(
                $"/api/trips/{trip.Id}/assign",
                new
                {
                    vehicleId = vehicle.VehicleId,
                    driverId = driverId
                });

        var assignBody =
            await assignResponse.Content.ReadAsStringAsync();

        Assert.True(
            assignResponse.IsSuccessStatusCode,
            $"Trip assignment failed. " +
            $"Status: {(int)assignResponse.StatusCode} " +
            $"{assignResponse.StatusCode}. " +
            $"Body: {assignBody}");

        // ============================================================
        // Arrange: Requested -> Assigned -> In-Progress
        // ============================================================

        using var driverClient =
            CreateAuthenticatedClient(driverToken);

        var respondResponse =
            await driverClient.PostAsJsonAsync(
                $"/api/trips/{trip.Id}/respond",
                new
                {
                    decision = "accept"
                });

        var respondBody =
            await respondResponse.Content.ReadAsStringAsync();

        Assert.True(
            respondResponse.IsSuccessStatusCode,
            $"Driver lifecycle response failed. " +
            $"Status: {(int)respondResponse.StatusCode} " +
            $"{respondResponse.StatusCode}. " +
            $"Body: {respondBody}");

        var finalTrip =
            JsonSerializer.Deserialize<TripDto>(
                respondBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidOperationException(
                "Trip response returned no trip data.");

        Assert.Equal(
            "In-Progress",
            finalTrip.Status);

        Assert.NotEqual(
            default,
            finalTrip.UpdatedAt);

        // ============================================================
        // Act: open the real frontend with Selenium
        // ============================================================

        Driver.Navigate()
            .GoToUrl(
                $"{FrontendBaseUrl}/login");

        Wait.Until(
            d =>
                d.FindElement(
                    By.Id("username")));

        Driver.FindElement(
                By.Id("username"))
            .SendKeys(
                driverUsername);

        Driver.FindElement(
                By.Id("password"))
            .SendKeys(
                driverPassword);

        Driver.FindElement(
                By.CssSelector(
                    "button[type='submit']"))
            .Click();

        Wait.Until(
            d =>
                d.Url.Contains(
                    "/driver/dashboard",
                    StringComparison.OrdinalIgnoreCase));

        Driver.Navigate()
            .GoToUrl(
                $"{FrontendBaseUrl}/driver/trips");

        // Wait for My Trips page
        Wait.Until(
            d =>
                d.FindElements(
                    By.XPath(
                        "//*[normalize-space()='My Trips']"))
                 .Count > 0);

        // ============================================================
        // Assert: transitioned trip is visible
        // ============================================================

        var row =
            Wait.Until(
                d =>
                {
                    var rows =
                        d.FindElements(
                            By.XPath(
                                "//tbody/tr"));

                    return rows.FirstOrDefault(
                        r =>
                            r.Text.Contains(
                                uniquePickup,
                                StringComparison.OrdinalIgnoreCase));
                });

        Assert.NotNull(
            row);

        var cells =
            row!.FindElements(
                By.TagName("td"));

        Assert.True(
            cells.Count >= 6,
            "Expected the My Trips table to contain at least 6 columns.");

        // Column positions:
        // 0 = Trip ID
        // 1 = Pickup
        // 2 = Drop-off
        // 3 = Capacity
        // 4 = Status
        // 5 = Updated

        var statusText =
            cells[4].Text.Trim();

        var updatedText =
            cells[5].Text.Trim();

        Assert.Equal(
            "In-Progress",
            statusText);

        Assert.False(
            string.IsNullOrWhiteSpace(updatedText),
            "Updated timestamp should be visible.");

        Assert.NotEqual(
            "—",
            updatedText);

        // ============================================================
        // Evidence screenshot
        // ============================================================

        ((ITakesScreenshot)Driver)
            .GetScreenshot()
            .SaveAsFile(
                "US13_Selenium_TripLifecycleStatusVisible_PASS.png");
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static HttpClient CreateAuthenticatedClient(
        string token)
    {
        var client =
            new HttpClient
            {
                BaseAddress =
                    new Uri(TripBaseUrl)
            };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        return client;
    }

    private static async Task CreateTestVehicleAsync(
        string adminToken)
    {
        using var fleetClient =
            new HttpClient
            {
                BaseAddress =
                    new Uri(FleetBaseUrl)
            };

        fleetClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var request =
            new
            {
                registrationNo =
                    $"SEL-{Guid.NewGuid():N}"
                        .Substring(0, 15),

                type = "Van",

                capacity = "10 persons"
            };

        var response =
            await fleetClient.PostAsJsonAsync(
                "/api/vehicles",
                request);

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode ||
            response.StatusCode ==
                HttpStatusCode.Conflict,
            $"Test vehicle creation failed. " +
            $"Status: {(int)response.StatusCode} " +
            $"{response.StatusCode}. " +
            $"Body: {body}");
    }

    private static async Task<Guid> GetDriverIdAsync(
        string username)
    {
        var (
            key,
            issuer,
            audience) =
                ReadJwtConfigFromLocalDevSettings();

        var adminToken =
            MintRoleToken(
                role: "Admin",
                subject: Guid.NewGuid(),
                key,
                issuer,
                audience);

        using var client =
            new HttpClient
            {
                BaseAddress =
                    new Uri(AuthBaseUrl)
            };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                adminToken);

        var response =
            await client.GetAsync(
                "/api/users/drivers");

        var body =
            await response.Content
                .ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Could not retrieve drivers. " +
            $"Status: {(int)response.StatusCode} " +
            $"{response.StatusCode}. " +
            $"Body: {body}");

        var drivers =
            JsonSerializer.Deserialize<
                List<DriverSummaryDto>>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? new List<DriverSummaryDto>();

        var driver =
            drivers.FirstOrDefault(
                d =>
                    string.Equals(
                        d.Username,
                        username,
                        StringComparison.OrdinalIgnoreCase));

        if (driver is null)
        {
            throw new InvalidOperationException(
                $"Could not find seeded Driver '{username}'.");
        }

        return driver.Id;
    }

    private static string MintRoleToken(
        string role,
        Guid subject,
        string? key = null,
        string? issuer = null,
        string? audience = null)
    {
        if (key is null ||
            issuer is null ||
            audience is null)
        {
            (
                key,
                issuer,
                audience) =
                    ReadJwtConfigFromLocalDevSettings();
        }

        var claims =
            new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    subject.ToString()),

                new(
                    JwtRegisteredClaimNames.UniqueName,
                    $"selenium_{role.ToLowerInvariant()}"),

                new(
                    JwtRegisteredClaimNames.Email,
                    $"selenium_{role.ToLowerInvariant()}@example.com"),

                new(
                    ClaimTypes.Role,
                    role)
            };

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

        var credentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires:
                    DateTime.UtcNow.AddMinutes(5),
                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    private static (
        string Key,
        string Issuer,
        string Audience)
        ReadJwtConfigFromLocalDevSettings()
    {
        var path =
            Path.Combine(
                AppContext.BaseDirectory,
                "../../../../../services/AuthUserService/appsettings.Development.json");

        path =
            Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Could not find JWT development settings at: {path}");
        }

        using var stream =
            File.OpenRead(path);

        using var doc =
            JsonDocument.Parse(stream);

        var jwt =
            doc.RootElement
                .GetProperty("Jwt");

        var key =
            jwt.GetProperty("Key")
                .GetString()
            ?? throw new InvalidOperationException(
                "Jwt:Key missing.");

        var issuer =
            jwt.GetProperty("Issuer")
                .GetString()
            ?? throw new InvalidOperationException(
                "Jwt:Issuer missing.");

        var audience =
            jwt.GetProperty("Audience")
                .GetString()
            ?? throw new InvalidOperationException(
                "Jwt:Audience missing.");

        return (
            key,
            issuer,
            audience);
    }

    // ============================================================
    // DTOs
    // ============================================================

    private sealed class DriverSummaryDto
    {
        public Guid Id { get; set; }

        public string Username { get; set; } =
            string.Empty;
    }

    private sealed class VehicleSuggestionDto
    {
        public Guid VehicleId { get; set; }

        public string RegistrationNo { get; set; } =
            string.Empty;

        public string Type { get; set; } =
            string.Empty;

        public string Capacity { get; set; } =
            string.Empty;
    }

    private sealed class TripDto
    {
        public Guid Id { get; set; }

        public string Status { get; set; } =
            string.Empty;

        public Guid? AssignedVehicleId { get; set; }

        public Guid? AssignedDriverId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}