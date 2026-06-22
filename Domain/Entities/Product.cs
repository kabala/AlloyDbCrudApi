using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class Product
{
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ListPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public decimal GetUnitMargin(decimal discount)
    {
        if (discount < 0m || discount > 1m)
            throw new InvalidDiscountException($"Discount must be between 0 and 1, got {discount}.");
        var net = ListPrice * (1m - discount);
        return net - CostPrice;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Category) || Category == "???")
            throw new InvalidProductException($"Product '{ProductId}' has invalid category '{Category}'.");
        if (string.IsNullOrWhiteSpace(Color))
            Color = "Unspecified";
        if (CostPrice < 0m)
            throw new InvalidProductException($"Product '{ProductId}' has negative cost price {CostPrice}.");
        if (ListPrice < 0m)
            throw new InvalidProductException($"Product '{ProductId}' has negative list price {ListPrice}.");
    }

    public void SoftDelete()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
    }
}
