using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TrackingTelemetryService.Data;
using TrackingTelemetryService.Services;

var builder = WebApplication.CreateBuilder(args);

// Machine-specific settings (database connection string, JWT key) live in
// appsettings.Local.json, which git ignores - see appsettings.Local.example.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();
builder.Services.AddScoped<TrackingService>();

// US-14: Trip Dispatch base URL
var tripBaseUrl = builder.Configuration["TripDispatchService:BaseUrl"]
    ?? throw new InvalidOperationException("TripDispatchService:BaseUrl is not configured.");

builder.Services.AddHttpClient("TripDispatch", client =>
{
    client.BaseAddress = new Uri(tripBaseUrl);
});
builder.Services.AddScoped<ITripDispatchClient, TripDispatchClient>();

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

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
