using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Returns;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class ReturnService : IReturnService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditService _audit;

    public ReturnService(AppDbContext db, ICurrentUserService current, IAuditService audit)
    {
        _db = db; _current = current; _audit = audit;
    }

    public async Task<ReturnDto> CreateAsync(CreateReturnRequest req, CancellationToken ct = default)
    {
        var sale = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.TransactionId == req.TransactionId, ct)
            ?? throw new InvalidReturnException($"Sale '{req.TransactionId}' not found.");
        if (sale.Status == Domain.Enums.SaleStatus.Returned)
            throw new InvalidReturnException($"Sale '{req.TransactionId}' is already returned.");

        var userId = _current.UserId ?? throw new UnauthorizedAccessException("Authenticated user required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            sale.MarkReturned();
            foreach (var item in sale.Items)
            {
                var inv = await _db.InventoryItems
                    .FirstOrDefaultAsync(i => i.StoreId == sale.StoreId && i.ProductId == item.ProductId, ct);
                if (inv is null)
                {
                    inv = new InventoryItem { StoreId = sale.StoreId, ProductId = item.ProductId, StockOnHand = 0 };
                    _db.InventoryItems.Add(inv);
                }
                inv.Increase(item.Quantity);
            }

            var ret = new Return
            {
                TransactionId = sale.TransactionId,
                Date = req.Date,
                Reason = req.Reason,
                Status = Domain.Enums.ReturnStatus.Approved,
                ApprovedByUserId = userId,
                ApprovedAt = DateTime.UtcNow,
                Notes = req.Notes,
            };
            _db.Returns.Add(ret);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.LogAsync("return.create", "Return", ret.Id.ToString(), $"Sale {sale.TransactionId} returned", ct);
            return new ReturnDto
            {
                Id = ret.Id, TransactionId = ret.TransactionId, Date = ret.Date, Reason = ret.Reason,
                Status = ret.Status, ApprovedByUserId = ret.ApprovedByUserId, Notes = ret.Notes,
                CreatedAt = ret.CreatedAt, ApprovedAt = ret.ApprovedAt,
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            throw new DomainException("Inventory was concurrently modified. Retry the return.");
        }
    }

    public async Task<ReturnDto?> GetAsync(Guid returnId, CancellationToken ct = default)
    {
        var r = await _db.Returns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == returnId, ct);
        if (r is null) return null;
        return new ReturnDto
        {
            Id = r.Id, TransactionId = r.TransactionId, Date = r.Date, Reason = r.Reason,
            Status = r.Status, ApprovedByUserId = r.ApprovedByUserId, Notes = r.Notes,
            CreatedAt = r.CreatedAt, ApprovedAt = r.ApprovedAt,
        };
    }
}
