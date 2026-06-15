using AlloyDbCrudApi.Data;
using AlloyDbCrudApi.Endpoints;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Cloud SQL for PostgreSQL uses the standard PostgreSQL wire protocol, so we use Npgsql.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddOpenApi();

var corsAllowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

if (corsAllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendOrigins", policy =>
        {
            policy
                .WithOrigins(corsAllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Auto-migrate in development for convenience.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (corsAllowedOrigins.Length > 0)
{
    app.UseCors("FrontendOrigins");
}

app.MapItemEndpoints();

app.Run();
