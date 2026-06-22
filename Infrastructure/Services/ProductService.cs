using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Products;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ProductService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<PagedResult<ProductDto>> ListAsync(ProductListQuery q, CancellationToken ct = default)
    {
        var qry = _db.Products.AsNoTracking();
        if (!q.IncludeInactive) qry = qry.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(q.Category)) qry = qry.Where(p => p.Category == q.Category);
        if (!string.IsNullOrWhiteSpace(q.Season)) qry = qry.Where(p => p.Season == q.Season);
        if (q.SupplierId.HasValue) qry = qry.Where(p => p.SupplierId == q.SupplierId);

        var total = await qry.CountAsync(ct);
        var items = await qry.OrderBy(p => p.ProductId)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                Category = p.Category,
                Color = p.Color,
                Size = p.Size,
                Season = p.Season,
                SupplierId = p.SupplierId,
                SupplierCode = p.Supplier!.Code,
                SupplierName = p.Supplier!.Name,
                CostPrice = p.CostPrice,
                ListPrice = p.ListPrice,
                IsActive = p.IsActive,
            })
            .ToListAsync(ct);
        return new PagedResult<ProductDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<ProductDto?> GetAsync(string productId, CancellationToken ct = default)
    {
        return await _db.Products.AsNoTracking()
            .Where(p => p.ProductId == productId)
            .Select(p => new ProductDto
            {
                ProductId = p.ProductId, Category = p.Category, Color = p.Color, Size = p.Size, Season = p.Season,
                SupplierId = p.SupplierId, SupplierCode = p.Supplier!.Code, SupplierName = p.Supplier!.Name,
                CostPrice = p.CostPrice, ListPrice = p.ListPrice, IsActive = p.IsActive,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest req, CancellationToken ct = default)
    {
        if (await _db.Products.AnyAsync(p => p.ProductId == req.ProductId, ct))
            throw new InvalidProductException($"Product '{req.ProductId}' already exists.");

        var product = new Product
        {
            ProductId = req.ProductId,
            Category = req.Category,
            Color = string.IsNullOrWhiteSpace(req.Color) ? "Unspecified" : req.Color,
            Size = req.Size,
            Season = req.Season,
            SupplierId = req.SupplierId,
            CostPrice = req.CostPrice,
            ListPrice = req.ListPrice,
        };
        product.Validate();
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("product.create", "Product", product.ProductId, $"Created {product.Category}", ct);
        return new ProductDto
        {
            ProductId = product.ProductId, Category = product.Category, Color = product.Color, Size = product.Size, Season = product.Season,
            SupplierId = product.SupplierId, CostPrice = product.CostPrice, ListPrice = product.ListPrice, IsActive = product.IsActive,
        };
    }
}
