using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class DiscountPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal MaxDiscount { get; set; } = 0.20m;
    public decimal MinMarginPercent { get; set; } = 0.20m;
    public bool RequiresSuperadminApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool Validate(Product product, decimal discount)
    {
        if (product is null)
            throw new DomainException("Discount policy requires a product.");
        if (discount < 0m || discount > 1m)
            throw new InvalidDiscountException($"Discount must be between 0 and 1, got {discount}.");
        if (discount > MaxDiscount)
            return false;

        var unitMargin = product.GetUnitMargin(discount);
        var revenue = product.ListPrice * (1m - discount);
        if (revenue <= 0m)
            return false;

        var marginPercent = unitMargin / revenue;
        return marginPercent >= MinMarginPercent || !RequiresSuperadminApproval;
    }

    public bool NeedsApproval(Product product, decimal discount)
    {
        if (product is null || discount <= 0m)
            return false;
        if (discount > MaxDiscount)
            return true;
        var unitMargin = product.GetUnitMargin(discount);
        var revenue = product.ListPrice * (1m - discount);
        if (revenue <= 0m)
            return true;
        var marginPercent = unitMargin / revenue;
        return marginPercent < MinMarginPercent && RequiresSuperadminApproval;
    }
}
