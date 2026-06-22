using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;
using Xunit;

namespace AlloyDbCrudApi.Tests.Domain;

public class ProductTests
{
    private static Product NewProduct(string category = "Tops", string color = "Black", decimal cost = 10m, decimal list = 20m)
        => new()
        {
            ProductId = "P_TEST",
            Category = category,
            Color = color,
            Size = "M",
            Season = "Spring",
            CostPrice = cost,
            ListPrice = list,
        };

    [Fact]
    public void GetUnitMargin_applies_discount_to_list_price_minus_cost()
    {
        var p = NewProduct(cost: 10m, list: 20m);
        var margin = p.GetUnitMargin(0.10m);
        Assert.Equal(20m * 0.9m - 10m, margin);
    }

    [Fact]
    public void GetUnitMargin_throws_when_discount_out_of_range()
    {
        var p = NewProduct();
        Assert.Throws<InvalidDiscountException>(() => p.GetUnitMargin(-0.1m));
        Assert.Throws<InvalidDiscountException>(() => p.GetUnitMargin(1.1m));
    }

    [Fact]
    public void Validate_rejects_invalid_category()
    {
        var p = NewProduct(category: "???");
        Assert.Throws<InvalidProductException>(() => p.Validate());
    }

    [Fact]
    public void Validate_rejects_empty_category()
    {
        var p = NewProduct(category: "");
        Assert.Throws<InvalidProductException>(() => p.Validate());
    }

    [Fact]
    public void Validate_normalizes_empty_color_to_unspecified()
    {
        var p = NewProduct(color: "");
        p.Validate();
        Assert.Equal("Unspecified", p.Color);
    }

    [Fact]
    public void Validate_rejects_negative_prices()
    {
        var p = NewProduct(cost: -1m);
        Assert.Throws<InvalidProductException>(() => p.Validate());
    }
}

public class SaleTests
{
    private static Product SampleProduct() => new()
    {
        ProductId = "P1",
        Category = "Tops",
        Color = "Black",
        Size = "M",
        Season = "Spring",
        CostPrice = 10m,
        ListPrice = 20m,
    };

    [Fact]
    public void AddItem_computes_revenue_and_margin_with_discount()
    {
        var sale = new Sale { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow), StoreId = "S1", CustomerId = "C1" };
        var p = SampleProduct();
        sale.AddItem(p, quantity: 3, discount: 0.10m);

        Assert.Single(sale.Items);
        Assert.Equal(3 * 20m * 0.9m, sale.TotalRevenue);
        Assert.Equal(3 * ((20m * 0.9m) - 10m), sale.TotalMargin);
        Assert.Equal(3, sale.TotalQuantity);
    }

    [Fact]
    public void AddItem_throws_for_non_positive_quantity()
    {
        var sale = new Sale { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow), StoreId = "S1", CustomerId = "C1" };
        Assert.Throws<DomainException>(() => sale.AddItem(SampleProduct(), 0, 0m));
    }

    [Fact]
    public void AddItem_throws_for_invalid_discount()
    {
        var sale = new Sale { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow), StoreId = "S1", CustomerId = "C1" };
        Assert.Throws<InvalidDiscountException>(() => sale.AddItem(SampleProduct(), 1, 1.5m));
    }

    [Fact]
    public void AddItem_validates_product_and_rejects_invalid_category()
    {
        var sale = new Sale { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow), StoreId = "S1", CustomerId = "C1" };
        var p = SampleProduct();
        p.Category = "???";
        Assert.Throws<InvalidProductException>(() => sale.AddItem(p, 1, 0m));
    }

    [Fact]
    public void MarkReturned_changes_status_and_throws_on_repeat()
    {
        var sale = new Sale { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow), StoreId = "S1", CustomerId = "C1" };
        sale.MarkReturned();
        Assert.Equal(SaleStatus.Returned, sale.Status);
        Assert.Throws<InvalidReturnException>(() => sale.MarkReturned());
    }
}

public class InventoryItemTests
{
    [Fact]
    public void Decrease_reduces_stock_on_hand()
    {
        var inv = new InventoryItem { ProductId = "P1", StoreId = "S1", StockOnHand = 10 };
        inv.Decrease(4);
        Assert.Equal(6, inv.StockOnHand);
    }

    [Fact]
    public void Decrease_throws_when_insufficient_stock()
    {
        var inv = new InventoryItem { ProductId = "P1", StoreId = "S1", StockOnHand = 3 };
        Assert.Throws<InsufficientStockException>(() => inv.Decrease(5));
    }

    [Fact]
    public void Decrease_considers_reserved_stock()
    {
        var inv = new InventoryItem { ProductId = "P1", StoreId = "S1", StockOnHand = 10, ReservedStock = 6 };
        Assert.Throws<InsufficientStockException>(() => inv.Decrease(5));
        inv.Decrease(4);
        Assert.Equal(6, inv.StockOnHand);
    }

    [Fact]
    public void Increase_adds_stock()
    {
        var inv = new InventoryItem { ProductId = "P1", StoreId = "S1", StockOnHand = 5 };
        inv.Increase(3);
        Assert.Equal(8, inv.StockOnHand);
    }

    [Fact]
    public void Decrease_throws_for_non_positive_quantity()
    {
        var inv = new InventoryItem { ProductId = "P1", StoreId = "S1", StockOnHand = 10 };
        Assert.Throws<DomainException>(() => inv.Decrease(0));
        Assert.Throws<DomainException>(() => inv.Decrease(-1));
    }
}

public class DiscountPolicyTests
{
    [Fact]
    public void Validate_rejects_discount_above_max()
    {
        var p = new Product { ProductId = "P1", Category = "Tops", Color = "Black", CostPrice = 10m, ListPrice = 20m };
        var policy = new DiscountPolicy { MaxDiscount = 0.20m, MinMarginPercent = 0.10m, RequiresSuperadminApproval = true };
        Assert.False(policy.Validate(p, 0.30m));
    }

    [Fact]
    public void Validate_rejects_when_margin_below_threshold_and_requires_approval()
    {
        var p = new Product { ProductId = "P1", Category = "Tops", Color = "Black", CostPrice = 18m, ListPrice = 20m };
        var policy = new DiscountPolicy { MaxDiscount = 0.50m, MinMarginPercent = 0.50m, RequiresSuperadminApproval = true };
        Assert.False(policy.Validate(p, 0.40m));
    }

    [Fact]
    public void NeedsApproval_true_when_discount_above_max()
    {
        var p = new Product { ProductId = "P1", Category = "Tops", Color = "Black", CostPrice = 10m, ListPrice = 20m };
        var policy = new DiscountPolicy { MaxDiscount = 0.10m, MinMarginPercent = 0.10m };
        Assert.True(policy.NeedsApproval(p, 0.20m));
    }

    [Fact]
    public void NeedsApproval_true_when_margin_below_threshold()
    {
        var p = new Product { ProductId = "P1", Category = "Tops", Color = "Black", CostPrice = 19m, ListPrice = 20m };
        var policy = new DiscountPolicy { MaxDiscount = 0.50m, MinMarginPercent = 0.30m, RequiresSuperadminApproval = true };
        Assert.True(policy.NeedsApproval(p, 0.40m));
    }
}

public class CustomerTests
{
    [Fact]
    public void ValidateProfile_normalizes_empty_email()
    {
        var c = new Customer { CustomerId = "C1", Age = 30, City = "Lisbon", Email = "" };
        c.ValidateProfile();
        Assert.Equal("unknown@local", c.Email);
    }

    [Fact]
    public void ValidateProfile_throws_for_invalid_age()
    {
        var c = new Customer { CustomerId = "C1", Age = 200, City = "Lisbon", Email = "a@b.com" };
        Assert.Throws<DomainException>(() => c.ValidateProfile());
    }
}

public class ReturnTests
{
    [Fact]
    public void Approve_sets_status_and_approver()
    {
        var r = new Return { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        var approver = Guid.NewGuid();
        r.Approve(approver, "ok");
        Assert.Equal(ReturnStatus.Approved, r.Status);
        Assert.Equal(approver, r.ApprovedByUserId);
        Assert.NotNull(r.ApprovedAt);
    }

    [Fact]
    public void Approve_throws_when_already_approved()
    {
        var r = new Return { TransactionId = "T1", Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        r.Approve(Guid.NewGuid());
        Assert.Throws<InvalidReturnException>(() => r.Approve(Guid.NewGuid()));
    }
}
