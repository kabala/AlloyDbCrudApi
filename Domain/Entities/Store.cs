using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Domain.Entities;

public class Store
{
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int StoreSizeM2 { get; set; }
    public StoreChannel Channel { get; set; } = StoreChannel.Physical;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<InventoryItem> Inventory { get; set; } = new();
    public List<Sale> Sales { get; set; } = new();
}
