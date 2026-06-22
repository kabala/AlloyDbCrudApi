using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Returns;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class ReturnEndpoints
{
    public static IEndpointRouteBuilder MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/returns").WithTags("Returns");

        g.MapGet("/{id:guid}", async (Guid id, IReturnService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } r ? Results.Ok(r) : Results.NotFound())
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"))
            .WithName("GetReturn");

        g.MapPost("/", async (CreateReturnRequest req, IReturnService svc, IValidator<CreateReturnRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try { var dto = await svc.CreateAsync(req, ct); return Results.Created($"/api/returns/{dto.Id}", dto); }
            catch (Domain.Exceptions.DomainException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized); }
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor")).WithName("CreateReturn");

        return app;
    }
}
