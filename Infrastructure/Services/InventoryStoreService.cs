using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Inventory;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    public InventoryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<InventoryDto>> ListAsync(InventoryQuery q, CancellationToken ct = default)
    {
        var qry = _db.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.StoreId)) qry = qry.Where(i => i.StoreId == q.StoreId);
        if (!string.IsNullOrWhiteSpace(q.ProductId)) qry = qry.Where(i => i.ProductId == q.ProductId);

        var total = await qry.CountAsync(ct);
        var items = await qry.OrderByDescending(i => i.UpdatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(i => new InventoryDto
            {
                Id = i.Id, ProductId = i.ProductId, StoreId = i.StoreId,
                StockOnHand = i.StockOnHand, ReservedStock = i.ReservedStock,
                AvailableStock = i.StockOnHand - i.ReservedStock, UpdatedAt = i.UpdatedAt,
            })
            .ToListAsync(ct);
        return new PagedResult<InventoryDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<InventoryDto?> GetAsync(string storeId, string productId, CancellationToken ct = default)
    {
        return await _db.InventoryItems.AsNoTracking()
            .Where(i => i.StoreId == storeId && i.ProductId == productId)
            .Select(i => new InventoryDto
            {
                Id = i.Id, ProductId = i.ProductId, StoreId = i.StoreId,
                StockOnHand = i.StockOnHand, ReservedStock = i.ReservedStock,
                AvailableStock = i.StockOnHand - i.ReservedStock, UpdatedAt = i.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class StoreService : IStoreService
{
    private readonly AppDbContext _db;
    public StoreService(AppDbContext db) => _db = db;

    public async Task<List<StoreDto>> ListAsync(CancellationToken ct = default)
    {
        return await _db.Stores.AsNoTracking()
            .OrderBy(s => s.StoreId)
            .Select(s => new StoreDto
            {
                StoreId = s.StoreId, StoreName = s.StoreName, Region = s.Region,
                StoreSizeM2 = s.StoreSizeM2, Channel = s.Channel, IsActive = s.IsActive,
            })
            .ToListAsync(ct);
    }
}
