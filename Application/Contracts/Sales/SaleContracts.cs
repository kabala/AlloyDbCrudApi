using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Application.Contracts.Sales;

public class SaleItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
}

public class CreateSaleRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<SaleItemRequest> Items { get; set; } = new();
}

public class SaleItemDto
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal UnitListPrice { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
}

public class SaleDto
{
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public SaleStatus Status { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal TotalDiscount { get; set; }
    public int TotalQuantity { get; set; }
    public List<SaleItemDto> Items { get; set; } = new();
}

public class SaleListQuery
{
    public string? StoreId { get; set; }
    public string? CustomerId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public SaleStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
