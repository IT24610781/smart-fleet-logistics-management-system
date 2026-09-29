using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using AuthUserService.Data;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Register application services

builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IUserAccountRepository>(sp => sp.GetRequiredService<UserRepository>());
builder.Services.AddHttpClient<DriverDeactivationService>(client =>
{
    var baseUrl = builder.Configuration["TripService:BaseUrl"]
        ?? (builder.Environment.IsDevelopment() ? "http://localhost:5294/" : null);
    if (baseUrl != null) client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IPasswordChallengeRepository, PasswordChallengeRepository>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddHttpClient("PasswordEmail", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<PasswordEmailQueue>();
builder.Services.AddSingleton<IPasswordEmailSender>(sp => sp.GetRequiredService<PasswordEmailQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PasswordEmailQueue>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) => await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many attempts. Please wait 15 minutes and try again." }, ct);
    foreach (var policy in new[] { (Name: "password-request", Limit: 5), (Name: "password-verify", Limit: 10) })
        options.AddPolicy(policy.Name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = policy.Limit, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});
builder.Services.AddScoped<AdminSeeder>();

// JWT Authentication

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is not configured.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = PasswordTokenEvents.Create();
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });


// Authorization


builder.Services.AddAuthorization();


// CORS

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173",
                          "http://127.0.0.1:5501")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// OpenAPI

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Build application

var app = builder.Build();


// HTTP request pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS
app.UseCors("Frontend");

app.UseAuthentication();

app.UseAuthorization();
app.UseRateLimiter();

// HTTPS
app.UseHttpsRedirection();

// Controllers
app.MapControllers();


// Start application

// Seed the initial Admin account if one doesn't exist yet
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<DatabaseConnection>();
    await using var connection = database.CreateConnection();
    await connection.OpenAsync();
    var migration = await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath, "password-security.sql"));
    await using var command = new MySqlConnector.MySqlCommand(migration, connection);
    await command.ExecuteNonQueryAsync();
    var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await adminSeeder.SeedAsync();
}

app.Run();
