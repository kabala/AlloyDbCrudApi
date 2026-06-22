using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Products;
using AlloyDbCrudApi.Application.Validators.Products;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/products").WithTags("Products");

        g.MapGet("/", async ([AsParameters] ProductListQuery q, IProductService svc, ProductListQueryValidator val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(q, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            return Results.Ok(await svc.ListAsync(q, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador")).WithName("ListProducts");

        g.MapGet("/{productId}", async (string productId, IProductService svc, CancellationToken ct) =>
            await svc.GetAsync(productId, ct) is { } p ? Results.Ok(p) : Results.NotFound())
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"))
            .WithName("GetProduct");

        g.MapPost("/", async (CreateProductRequest req, IProductService svc, IValidator<CreateProductRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try { var dto = await svc.CreateAsync(req, ct); return Results.Created($"/api/products/{dto.ProductId}", dto); }
            catch (Domain.Exceptions.InvalidProductException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin")).WithName("CreateProduct");

        return app;
    }
}
