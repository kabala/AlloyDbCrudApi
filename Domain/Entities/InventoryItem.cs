using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProductId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public int StockOnHand { get; set; }
    public int ReservedStock { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int AvailableStock => StockOnHand - ReservedStock;

    public Store? Store { get; set; }
    public Product? Product { get; set; }

    public void Decrease(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Decrease quantity must be positive.");
        if (quantity > AvailableStock)
            throw new InsufficientStockException(ProductId, StoreId, quantity, AvailableStock);
        StockOnHand -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Increase(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Increase quantity must be positive.");
        StockOnHand += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Reserve quantity must be positive.");
        if (quantity > AvailableStock)
            throw new InsufficientStockException(ProductId, StoreId, quantity, AvailableStock);
        ReservedStock += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
