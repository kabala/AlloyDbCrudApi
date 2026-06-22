namespace AlloyDbCrudApi.Application.Contracts.Products;

public class ProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierCode { get; set; }
    public string? SupplierName { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ListPrice { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal ListPrice { get; set; }
}

public class ProductListQuery
{
    public string? Category { get; set; }
    public string? Season { get; set; }
    public Guid? SupplierId { get; set; }
    public bool IncludeInactive { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
