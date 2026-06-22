using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Sales;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditService _audit;

    public SaleService(AppDbContext db, ICurrentUserService current, IAuditService audit)
    {
        _db = db; _current = current; _audit = audit;
    }

    public async Task<PagedResult<SaleDto>> ListAsync(SaleListQuery q, CancellationToken ct = default)
    {
        var qry = _db.Sales.AsNoTracking().Include(s => s.Items).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.StoreId)) qry = qry.Where(s => s.StoreId == q.StoreId);
        if (!string.IsNullOrWhiteSpace(q.CustomerId)) qry = qry.Where(s => s.CustomerId == q.CustomerId);
        if (q.FromDate.HasValue) qry = qry.Where(s => s.Date >= q.FromDate);
        if (q.ToDate.HasValue) qry = qry.Where(s => s.Date <= q.ToDate);
        if (q.Status.HasValue) qry = qry.Where(s => s.Status == q.Status);

        var total = await qry.CountAsync(ct);
        var sales = await qry.OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.TransactionId)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .ToListAsync(ct);
        var storeIds = sales.Select(s => s.StoreId).Distinct().ToList();
        var stores = await _db.Stores.AsNoTracking()
            .Where(st => storeIds.Contains(st.StoreId))
            .ToDictionaryAsync(st => st.StoreId, st => st.StoreName, ct);
        var items = sales.Select(s => ToDto(s, stores.GetValueOrDefault(s.StoreId) ?? s.StoreId)).ToList();
        return new PagedResult<SaleDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<SaleDto?> GetAsync(string transactionId, CancellationToken ct = default)
    {
        var s = await _db.Sales.AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);
        if (s is null) return null;
        var storeName = await _db.Stores.AsNoTracking()
            .Where(st => st.StoreId == s.StoreId)
            .Select(st => st.StoreName)
            .FirstOrDefaultAsync(ct) ?? s.StoreId;
        return ToDto(s, storeName);
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest req, CancellationToken ct = default)
    {
        if (await _db.Sales.AnyAsync(s => s.TransactionId == req.TransactionId, ct))
            throw new DomainException($"Sale '{req.TransactionId}' already exists.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CustomerId == req.CustomerId, ct)
            ?? throw new DomainException($"Customer '{req.CustomerId}' not found.");
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.StoreId == req.StoreId, ct)
            ?? throw new DomainException($"Store '{req.StoreId}' not found.");
        if (!store.IsActive)
            throw new DomainException($"Store '{req.StoreId}' is not active.");

        var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, ct);

        var discountPolicy = await _db.DiscountPolicies.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync(d => d.IsActive, ct)
            ?? new DiscountPolicy();

        var userId = _current.UserId ?? throw new UnauthorizedAccessException("Authenticated user required.");

        var sale = new Sale
        {
            TransactionId = req.TransactionId,
            Date = req.Date,
            StoreId = store.StoreId,
            CustomerId = customer.CustomerId,
            CreatedByUserId = userId,
        };

        var inventoryLocks = new List<(InventoryItem Item, int Qty)>();
        foreach (var item in req.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new InvalidProductException($"Product '{item.ProductId}' not found.");
            if (!product.IsActive)
                throw new InvalidProductException($"Product '{item.ProductId}' is not active.");

            if (discountPolicy.NeedsApproval(product, item.Discount) && !_current.IsInRole(RoleNames.Superadmin))
                throw new InvalidDiscountException($"Discount {item.Discount} on '{item.ProductId}' requires Superadmin approval.");

            sale.AddItem(product, item.Quantity, item.Discount);

            var inv = await _db.InventoryItems
                .FirstOrDefaultAsync(i => i.StoreId == store.StoreId && i.ProductId == item.ProductId, ct);
            if (inv is null)
            {
                inv = new InventoryItem { StoreId = store.StoreId, ProductId = item.ProductId, StockOnHand = 0 };
                _db.InventoryItems.Add(inv);
            }
            inv.Decrease(item.Quantity);
            inventoryLocks.Add((inv, item.Quantity));
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new DomainException("Inventory was concurrently modified. Retry the sale.");
        }
        await _audit.LogAsync("sale.create", "Sale", sale.TransactionId, $"TotalRevenue={sale.TotalRevenue:0.00}", ct);
        return ToDto(sale, store.StoreName);
    }

    private static SaleDto ToDto(Sale s, string storeName) => new()
    {
        TransactionId = s.TransactionId,
        Date = s.Date,
        StoreId = s.StoreId,
        StoreName = storeName,
        CustomerId = s.CustomerId,
        Status = s.Status,
        TotalRevenue = s.TotalRevenue,
        TotalMargin = s.TotalMargin,
        TotalDiscount = s.TotalDiscount,
        TotalQuantity = s.TotalQuantity,
        Items = s.Items.Select(i => new SaleItemDto
        {
            Id = i.Id, ProductId = i.ProductId, Quantity = i.Quantity, Discount = i.Discount,
            UnitListPrice = i.UnitListPrice, UnitCostPrice = i.UnitCostPrice,
            Revenue = i.Revenue, Margin = i.Margin,
        }).ToList(),
    };
}
