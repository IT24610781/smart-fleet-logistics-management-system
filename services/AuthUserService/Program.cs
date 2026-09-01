using AuthUserService.Data;
using AuthUserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddControllers();
builder.Services.AddSingleton<DatabaseConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtService>();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();