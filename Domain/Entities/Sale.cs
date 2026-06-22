using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class Sale
{
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public decimal TotalRevenue { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal TotalDiscount { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Store? Store { get; set; }
    public Customer? Customer { get; set; }
    public User? CreatedBy { get; set; }
    public List<SaleItem> Items { get; set; } = new();
    public Return? Return { get; set; }

    public void AddItem(Product product, int quantity, decimal discount)
    {
        if (product is null)
            throw new DomainException("Cannot add a null product to a sale.");
        if (quantity <= 0)
            throw new DomainException("Sale item quantity must be positive.");
        if (discount < 0m || discount > 1m)
            throw new InvalidDiscountException($"Discount must be between 0 and 1, got {discount}.");

        product.Validate();

        var unitList = product.ListPrice;
        var unitCost = product.CostPrice;
        var revenue = quantity * unitList * (1m - discount);
        var margin = quantity * ((unitList * (1m - discount)) - unitCost);

        Items.Add(new SaleItem
        {
            ProductId = product.ProductId,
            Quantity = quantity,
            Discount = discount,
            UnitListPrice = unitList,
            UnitCostPrice = unitCost,
            Revenue = revenue,
            Margin = margin,
        });

        TotalRevenue += revenue;
        TotalMargin += margin;
        TotalDiscount += discount * quantity;
        TotalQuantity += quantity;
    }

    public void MarkReturned()
    {
        if (Status == SaleStatus.Returned)
            throw new InvalidReturnException($"Sale '{TransactionId}' is already returned.");
        Status = SaleStatus.Returned;
    }

    public void RecalculateTotals()
    {
        TotalRevenue = Items.Sum(i => i.Revenue);
        TotalMargin = Items.Sum(i => i.Margin);
        TotalDiscount = Items.Sum(i => i.Discount * i.Quantity);
        TotalQuantity = Items.Sum(i => i.Quantity);
    }
}
