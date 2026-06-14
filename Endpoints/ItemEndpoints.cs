using AlloyDbCrudApi.Data;
using AlloyDbCrudApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Endpoints;

public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/items")
            .WithTags("Items");

        group.MapGet("/", async (AppDbContext db) =>
            await db.Items.AsNoTracking().OrderByDescending(i => i.CreatedAt).ToListAsync())
            .WithName("ListItems");

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
            await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id)
                is { } item
                ? Results.Ok(item)
                : Results.NotFound())
            .WithName("GetItemById");

        group.MapPost("/", async ([FromBody] Item item, AppDbContext db) =>
        {
            item.Id = 0;
            item.CreatedAt = DateTime.UtcNow;
            db.Items.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/items/{item.Id}", item);
        })
            .WithName("CreateItem");

        group.MapPut("/{id:int}", async (int id, [FromBody] Item updated, AppDbContext db) =>
        {
            var existing = await db.Items.FindAsync(id);
            if (existing is null)
                return Results.NotFound();

            existing.Name = updated.Name;
            existing.Description = updated.Description;
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("UpdateItem");

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var existing = await db.Items.FindAsync(id);
            if (existing is null)
                return Results.NotFound();

            db.Items.Remove(existing);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("DeleteItem");

        return app;
    }
}
