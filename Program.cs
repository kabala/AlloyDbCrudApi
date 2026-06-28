using System.Text;
using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Validators.Auth;
using AlloyDbCrudApi.Application.Validators.Customers;
using AlloyDbCrudApi.Application.Validators.Products;
using AlloyDbCrudApi.Application.Validators.Returns;
using AlloyDbCrudApi.Application.Validators.Sales;
using AlloyDbCrudApi.Application.Validators.Users;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Endpoints;
using AlloyDbCrudApi.Infrastructure.Identity;
using AlloyDbCrudApi.Infrastructure.Persistence;
using AlloyDbCrudApi.Infrastructure.Seeding;
using AlloyDbCrudApi.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.CorrelationId;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console());

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) && builder.Environment.IsDevelopment())
{
    jwt = jwt with { SigningKey = "dev-only-signing-key-please-override-in-production-32bytes-min" };
    builder.Services.Configure<JwtOptions>(opts => opts.SigningKey = jwt.SigningKey);
}
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddIdentity<User, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(opts =>
{
    opts.Password.RequireDigit = true;
    opts.Password.RequireUppercase = true;
    opts.Password.RequireNonAlphanumeric = true;
    opts.Password.RequiredLength = 8;
    opts.Lockout.MaxFailedAccessAttempts = 5;
    opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    opts.User.RequireUniqueEmail = true;
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();
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
            policy.WithOrigins(corsAllowedOrigins).AllowAnyHeader().AllowAnyMethod();
        });
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IStoreService, StoreService>();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSaleRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateReturnRequestValidator>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

builder.Services.AddScoped<RetailSeeder>();
builder.Services.AddScoped<RetailHistorySeeder>();

var app = builder.Build();

var seedCommand = SeedCommand.Parse(args);
if (seedCommand is not null)
{
    await RunSeedCommandAsync(app, seedCommand);
    return;
}

app.UseSerilogRequestLogging();

app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Items["CorrelationId"] = correlationId;
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var seeder = sp.GetRequiredService<RetailSeeder>();
    await seeder.SeedAsync();
}

if (corsAllowedOrigins.Length > 0)
    app.UseCors("FrontendOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("ready"),
});

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapProductEndpoints();
app.MapCustomerEndpoints();
app.MapSaleEndpoints();
app.MapReturnEndpoints();
app.MapInventoryEndpoints();

app.Run();

static async Task RunSeedCommandAsync(WebApplication app, SeedCommand command)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    switch (command.Name)
    {
        case SeedCommand.RetailBiHistory:
            var retailHistorySeeder = services.GetRequiredService<RetailHistorySeeder>();
            await retailHistorySeeder.SeedAsync();
            return;
        default:
            throw new InvalidOperationException($"Unsupported seed command '{command.Name}'.");
    }
}

public partial class Program;
