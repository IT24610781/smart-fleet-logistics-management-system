using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TripDispatchService.Data;
using TripDispatchService.Services;

var builder = WebApplication.CreateBuilder(args);

// Machine-specific settings (database connection string, JWT key) live in
// appsettings.Local.json, which git ignores - see appsettings.Local.example.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Register application services
builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<TripCompletionService>();
builder.Services.AddScoped<IDriverDirectoryClient, DriverDirectoryClient>();
builder.Services.AddScoped<DriverPerformanceService>();
builder.Services.AddScoped<DriverEligibilityClient>();
builder.Services.AddScoped<DriverAssignmentGuard>();

// US-10: Fleet base URL - same pattern as the DB connection string, read
var fleetBaseUrl = builder.Configuration["FleetService:BaseUrl"]
    ?? throw new InvalidOperationException("FleetService:BaseUrl is not configured.");

builder.Services.AddHttpClient<IFleetVehicleClient, FleetVehicleClient>(client =>
{
    client.BaseAddress = new Uri(fleetBaseUrl);
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = AccountTokenEvents.Create();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddHttpClient("AuthAccountStatus", client =>
{
    var baseUrl = builder.Configuration["AuthService:BaseUrl"]
        ?? (builder.Environment.IsDevelopment() ? "http://localhost:5100/" : null);
    if (baseUrl != null) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Authorization
builder.Services.AddAuthorization();

var frontendOrigins = new List<string>
{
    "http://localhost:5173",
    "http://localhost:5174",
    "http://127.0.0.1:5501"
};

var deployedFrontendOrigin = builder.Configuration["Frontend:Origin"];
if (!string.IsNullOrWhiteSpace(deployedFrontendOrigin))
{
    frontendOrigins.Add(deployedFrontendOrigin.TrimEnd('/'));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(frontendOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS must be before controllers/endpoints
app.UseCors("Frontend");

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
