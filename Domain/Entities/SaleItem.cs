namespace AlloyDbCrudApi.Domain.Entities;

public class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal UnitListPrice { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }

    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
}
