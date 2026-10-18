using System.Text;
using FleetVehicleService.Data;
using FleetVehicleService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddSingleton<IVehicleEventPublisher, KafkaVehicleEventPublisher>();
builder.Services.AddScoped<ITripEligibilityChecker, StubTripEligibilityChecker>();

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
