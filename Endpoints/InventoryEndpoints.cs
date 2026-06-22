using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Inventory;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory").WithTags("Inventory");

        g.MapGet("/", async ([AsParameters] InventoryQuery q, IInventoryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(q, ct)))
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor"))
            .WithName("ListInventory");

        g.MapGet("/by", async ([AsParameters] InventoryLookupQuery q, IInventoryService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q.StoreId) || string.IsNullOrWhiteSpace(q.ProductId))
                return Results.BadRequest(new { error = "storeId and productId are required." });
            return await svc.GetAsync(q.StoreId, q.ProductId, ct) is { } i ? Results.Ok(i) : Results.NotFound();
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor")).WithName("GetInventoryItem");

        g.MapGet("/stores", async (IStoreService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)))
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"))
            .WithName("ListStores");

        return app;
    }
}

public class InventoryLookupQuery
{
    public string StoreId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
}
