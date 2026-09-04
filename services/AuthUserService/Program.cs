using AuthUserService.Data;
using AuthUserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddControllers();
builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AdminSeeder>();

// CORS - allow the React/Vite frontend
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

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var adminSeeder = scope.ServiceProvider
        .GetRequiredService<AdminSeeder>();

    await adminSeeder.SeedAsync();
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// CORS must be before controllers/endpoints
app.UseCors("Frontend");

app.UseHttpsRedirection();

app.MapControllers();

app.Run();