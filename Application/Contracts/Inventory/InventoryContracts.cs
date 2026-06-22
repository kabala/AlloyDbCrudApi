namespace AlloyDbCrudApi.Application.Contracts.Inventory;

public class InventoryDto
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public int StockOnHand { get; set; }
    public int ReservedStock { get; set; }
    public int AvailableStock { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InventoryQuery
{
    public string? StoreId { get; set; }
    public string? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
