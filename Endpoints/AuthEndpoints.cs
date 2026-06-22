using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AlloyDbCrudApi.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/login", async (LoginRequest req, IAuthService auth, IValidator<LoginRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var t = await auth.LoginAsync(req, ct);
                return Results.Ok(t);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }).AllowAnonymous().WithName("Login");

        g.MapPost("/refresh", async (RefreshRequest req, IAuthService auth, IValidator<RefreshRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try
            {
                var t = await auth.RefreshAsync(req, ct);
                return Results.Ok(t);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }).AllowAnonymous().WithName("RefreshToken");

        return app;
    }
}
