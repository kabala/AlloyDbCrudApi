using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Users;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").WithTags("Users");

        g.MapGet("/", async ([AsParameters] UserListQuery q, IUserService svc, CancellationToken ct) =>
        {
            var page = q.Page < 1 ? 1 : q.Page;
            var size = q.PageSize is < 1 or > 200 ? 25 : q.PageSize;
            return Results.Ok(await svc.ListAsync(page, size, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin")).WithName("ListUsers");

        g.MapGet("/{id:guid}", async (Guid id, IUserService svc, CancellationToken ct) =>
            await svc.GetByIdAsync(id, ct) is { } u ? Results.Ok(u) : Results.NotFound())
            .RequireAuthorization(pb => pb.RequireRole("Superadmin"))
            .WithName("GetUser");

        g.MapPost("/", async (CreateUserRequest req, IUserService svc, CancellationToken ct) =>
        {
            try { return Results.Created($"/api/users/{Guid.NewGuid()}", await svc.CreateAsync(req, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin")).WithName("CreateUser");

        return app;
    }
}

public class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
