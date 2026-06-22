using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Returns;
using AlloyDbCrudApi.Application.Contracts.Sales;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using AlloyDbCrudApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlloyDbCrudApi.Tests.Application;

public abstract class IntegrationTestBase
{
    protected static async Task<AppDbContext> BuildInMemoryContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    protected static ICurrentUserService FakeCurrentUser(Guid userId, params string[] roles)
        => new FakeCurrentUserService(userId, roles);

    protected static IAuditService NoOpAudit() => new NoOpAuditService();

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly Guid _id;
        private readonly HashSet<string> _roles;
        public FakeCurrentUserService(Guid id, string[] roles) { _id = id; _roles = roles.ToHashSet(); }
        public Guid? UserId => _id;
        public string? Email => "test@local";
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => _roles.Contains(role);
        public IReadOnlyList<string> Roles => _roles.ToList();
        public string? CorrelationId => "test";
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(string action, string entityName, string entityId, string? detail = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}

public class SaleServiceIntegrationTests : IntegrationTestBase
{
    private static async Task<(AppDbContext db, SaleService svc)> BuildServiceAsync(string[] roles)
    {
        var db = await BuildInMemoryContextAsync();
        var store = new Store { StoreId = "S1", StoreName = "Store1", Region = "Lisbon", Channel = StoreChannel.Physical };
        var customer = new Customer { CustomerId = "C1", Age = 30, City = "Lisbon", Email = "c@b.com", Gender = Gender.Female };
        var product = new Product { ProductId = "P1", Category = "Tops", Color = "Black", Size = "M", Season = "Spring", CostPrice = 10m, ListPrice = 20m };
        var inv = new InventoryItem { StoreId = "S1", ProductId = "P1", StockOnHand = 100 };
        var user = new User { FullName = "Seller", Email = "s@b.com", UserName = "s@b.com", Role = Role.Vendedor };
        var policy = new DiscountPolicy { Name = "Default", MaxDiscount = 0.30m, MinMarginPercent = 0.10m, RequiresSuperadminApproval = true, IsActive = true };
        db.Stores.Add(store);
        db.Customers.Add(customer);
        db.Products.Add(product);
        db.InventoryItems.Add(inv);
        db.DiscountPolicies.Add(policy);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = new SaleService(db, FakeCurrentUser(user.Id, roles), NoOpAudit());
        return (db, svc);
    }

    [Fact]
    public async Task CreateAsync_persists_sale_and_decreases_inventory()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Vendedor" });
        var req = new CreateSaleRequest
        {
            TransactionId = "T_TEST",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 3, Discount = 0.10m } },
        };

        var dto = await svc.CreateAsync(req);

        Assert.Equal("T_TEST", dto.TransactionId);
        Assert.Equal(3 * 20m * 0.9m, dto.TotalRevenue);
        Assert.Equal(3, dto.TotalQuantity);
        var inv = await db.InventoryItems.FirstAsync(i => i.StoreId == "S1" && i.ProductId == "P1");
        Assert.Equal(97, inv.StockOnHand);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_customer()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Vendedor" });
        var req = new CreateSaleRequest
        {
            TransactionId = "T_X",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "UNKNOWN",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 1, Discount = 0m } },
        };
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(req));
    }

    [Fact]
    public async Task CreateAsync_rejects_high_discount_without_superadmin()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Vendedor" });
        var req = new CreateSaleRequest
        {
            TransactionId = "T_X",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 1, Discount = 0.95m } },
        };
        await Assert.ThrowsAsync<InvalidDiscountException>(() => svc.CreateAsync(req));
    }

    [Fact]
    public async Task CreateAsync_allows_high_discount_when_superadmin()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Superadmin" });
        var req = new CreateSaleRequest
        {
            TransactionId = "T_OK",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 1, Discount = 0.95m } },
        };
        var dto = await svc.CreateAsync(req);
        Assert.Equal("T_OK", dto.TransactionId);
    }

    [Fact]
    public async Task CreateAsync_rejects_insufficient_stock()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Vendedor" });
        var req = new CreateSaleRequest
        {
            TransactionId = "T_BIG",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 1000, Discount = 0m } },
        };
        await Assert.ThrowsAsync<InsufficientStockException>(() => svc.CreateAsync(req));
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_transaction()
    {
        var (db, svc) = await BuildServiceAsync(new[] { "Vendedor" });
        var req = new CreateSaleRequest
        {
            TransactionId = "DUP",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            Items = { new SaleItemRequest { ProductId = "P1", Quantity = 1, Discount = 0m } },
        };
        await svc.CreateAsync(req);
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(req));
    }
}

public class ReturnServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateAsync_marks_sale_returned_and_restores_inventory()
    {
        var db = await BuildInMemoryContextAsync();
        var store = new Store { StoreId = "S1", StoreName = "Store1", Region = "Lisbon", Channel = StoreChannel.Physical };
        var customer = new Customer { CustomerId = "C1", Age = 30, City = "Lisbon", Email = "c@b.com" };
        var product = new Product { ProductId = "P1", Category = "Tops", Color = "Black", CostPrice = 10m, ListPrice = 20m };
        var user = new User { FullName = "Seller", Email = "s@b.com", UserName = "s@b.com", Role = Role.Vendedor };
        db.Stores.Add(store); db.Customers.Add(customer); db.Products.Add(product); db.Users.Add(user);
        var sale = new Sale
        {
            TransactionId = "T_R",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            CreatedByUserId = user.Id,
            Status = SaleStatus.Completed,
            TotalRevenue = 20m,
            TotalMargin = 10m,
            TotalQuantity = 1,
            Items = { new SaleItem { ProductId = "P1", Quantity = 1, Discount = 0, UnitListPrice = 20m, UnitCostPrice = 10m, Revenue = 20m, Margin = 10m } },
        };
        var inv = new InventoryItem { StoreId = "S1", ProductId = "P1", StockOnHand = 5 };
        db.Sales.Add(sale); db.InventoryItems.Add(inv);
        await db.SaveChangesAsync();

        var svc = new ReturnService(db, FakeCurrentUser(user.Id, "Vendedor"), NoOpAudit());
        var dto = await svc.CreateAsync(new CreateReturnRequest
        {
            TransactionId = "T_R",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Reason = ReturnReason.CustomerChange,
        });

        Assert.Equal(ReturnStatus.Approved, dto.Status);
        var updatedSale = await db.Sales.FirstAsync(s => s.TransactionId == "T_R");
        Assert.Equal(SaleStatus.Returned, updatedSale.Status);
        var updatedInv = await db.InventoryItems.FirstAsync(i => i.StoreId == "S1" && i.ProductId == "P1");
        Assert.Equal(6, updatedInv.StockOnHand);
    }

    [Fact]
    public async Task CreateAsync_rejects_return_for_unknown_sale()
    {
        var db = await BuildInMemoryContextAsync();
        var user = new User { FullName = "Seller", Email = "s@b.com", UserName = "s@b.com", Role = Role.Vendedor };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var svc = new ReturnService(db, FakeCurrentUser(user.Id, "Vendedor"), NoOpAudit());
        await Assert.ThrowsAsync<InvalidReturnException>(() => svc.CreateAsync(new CreateReturnRequest
        {
            TransactionId = "NOPE",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
        }));
    }

    [Fact]
    public async Task CreateAsync_rejects_double_return()
    {
        var db = await BuildInMemoryContextAsync();
        var user = new User { FullName = "Seller", Email = "s@b.com", UserName = "s@b.com", Role = Role.Vendedor };
        var sale = new Sale
        {
            TransactionId = "T_DBL",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StoreId = "S1",
            CustomerId = "C1",
            CreatedByUserId = Guid.Empty,
            Status = SaleStatus.Returned,
            Items = { new SaleItem { ProductId = "P1", Quantity = 1, Discount = 0, UnitListPrice = 1, UnitCostPrice = 0, Revenue = 1, Margin = 1 } },
        };
        db.Users.Add(user); db.Sales.Add(sale);
        await db.SaveChangesAsync();
        var svc = new ReturnService(db, FakeCurrentUser(user.Id, "Vendedor"), NoOpAudit());
        await Assert.ThrowsAsync<InvalidReturnException>(() => svc.CreateAsync(new CreateReturnRequest
        {
            TransactionId = "T_DBL",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
        }));
    }
}
