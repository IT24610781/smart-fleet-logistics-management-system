using System.Text;
using AuthUserService.Data;
using AuthUserService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Register application services
// ============================================================

builder.Services.AddControllers();

builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminSeeder>();


// ============================================================
// JWT Authentication
// ============================================================

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


// ============================================================
// Authorization
// ============================================================

builder.Services.AddAuthorization();


// ============================================================
// CORS
// ============================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ============================================================
// OpenAPI
// ============================================================

builder.Services.AddOpenApi();


// ============================================================
// Build application
// ============================================================

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await adminSeeder.SeedAsync();
}


// ============================================================
// HTTP request pipeline
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS
app.UseCors("Frontend");

// Authentication must come before Authorization
app.UseAuthentication();

app.UseAuthorization();

// HTTPS
app.UseHttpsRedirection();

// Controllers
app.MapControllers();


// ============================================================
// Start application
// ============================================================

app.Run();
