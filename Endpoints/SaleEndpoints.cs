using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Sales;
using AlloyDbCrudApi.Application.Validators.Sales;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSaleEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sales").WithTags("Sales");

        g.MapGet("/", async ([AsParameters] SaleListQuery q, ISaleService svc, SaleListQueryValidator val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(q, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            return Results.Ok(await svc.ListAsync(q, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador")).WithName("ListSales");

        g.MapGet("/{transactionId}", async (string transactionId, ISaleService svc, CancellationToken ct) =>
            await svc.GetAsync(transactionId, ct) is { } s ? Results.Ok(s) : Results.NotFound())
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"))
            .WithName("GetSale");

        g.MapPost("/", async (CreateSaleRequest req, ISaleService svc, IValidator<CreateSaleRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try { var dto = await svc.CreateAsync(req, ct); return Results.Created($"/api/sales/{dto.TransactionId}", dto); }
            catch (Domain.Exceptions.DomainException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Json(new { error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized); }
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor")).WithName("CreateSale");

        return app;
    }
}
