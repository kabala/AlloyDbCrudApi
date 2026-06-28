using System.Data.Common;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlloyDbCrudApi.Infrastructure.Seeding;

public class RetailHistorySeeder
{
    private const int ProductCount = 50_000;
    private const int CustomerCount = 25_000;
    private const int SaleCount = 43_489;
    private const int ReturnCount = 4_317;
    private const int BatchSize = 1_000;
    private const decimal TargetRevenue = 10_799_676.18m;
    private const decimal TargetMargin = 6_164_273.00m;
    private static readonly DateOnly StartDate = new(2020, 1, 1);
    private static readonly DateOnly EndDate = new(2024, 12, 31);

    private static readonly string[] CategorySequence = ["Bottoms", "Tops", "Accessories", "Shoes", "Dresses"];
    private static readonly string[] ColorPalette = ["Black", "White", "Navy", "Beige", "Green", "Red", "Blue", "Gray", "Brown", "Pink"];
    private static readonly string[] SizePalette = ["XS", "S", "M", "L", "XL"];
    private static readonly string[] SeasonPalette = ["Spring", "Summer", "Autumn", "Winter"];
    private static readonly string[] SupplierCodes = ["suppliera", "supplierb", "supplierc", "supplierd"];
    private static readonly string[] CityPalette = ["Lisbon", "Coimbra", "Faro", "Braga", "Porto"];
    private static readonly ReturnReason[] ReturnReasonPalette =
    [
        ReturnReason.CustomerChange,
        ReturnReason.WrongSize,
        ReturnReason.Defective,
        ReturnReason.WrongItem,
        ReturnReason.Other,
    ];

    private readonly AppDbContext _db;
    private readonly UserManager<User> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ILogger<RetailHistorySeeder> _log;

    public RetailHistorySeeder(
        AppDbContext db,
        UserManager<User> users,
        RoleManager<IdentityRole<Guid>> roles,
        ILogger<RetailHistorySeeder> log)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var beforeSize = await TryGetDatabaseSizeAsync(ct);
        await SeedRolesAsync(ct);
        var seededUsers = await SeedUsersAsync(ct);
        var approverUserId = seededUsers.SuperadminId;
        var createdByUserId = seededUsers.SellerId;
        await SeedDiscountPolicyAsync(ct);

        var plan = BuildPlan(createdByUserId, approverUserId);

        await SeedSuppliersAsync(plan.Suppliers, ct);
        await SeedStoresAsync(plan.Stores, ct);
        await SeedProductsAsync(plan.Products, ct);
        await SeedCustomersAsync(plan.Customers, ct);
        await SeedSalesAsync(plan.Sales, ct);
        await SeedInventoryAsync(plan.Inventory, ct);

        var afterSize = await TryGetDatabaseSizeAsync(ct);
        _log.LogInformation(
            "Retail BI history seed complete. Stores={Stores} Suppliers={Suppliers} Products={Products} Customers={Customers} Sales={Sales} Returns={Returns} Revenue={Revenue:0.00} Margin={Margin:0.00} DbSizeBefore={BeforeSize} DbSizeAfter={AfterSize}",
            plan.Stores.Count,
            plan.Suppliers.Count,
            plan.Products.Count,
            plan.Customers.Count,
            plan.Sales.Count,
            plan.Sales.Count(s => s.Return is not null),
            plan.Sales.Sum(s => s.TotalRevenue),
            plan.Sales.Sum(s => s.TotalMargin),
            FormatBytes(beforeSize),
            FormatBytes(afterSize));
    }

    private SeedPlan BuildPlan(Guid createdByUserId, Guid approverUserId)
    {
        var random = new Random(20260627);
        var stores = BuildStores();
        var suppliers = BuildSuppliers();
        var products = BuildProducts(random);
        var customers = BuildCustomers(random);

        var discounts = BuildDiscounts(random);
        var quantities = BuildQuantities(random);
        var returns = BuildReturns(random);
        var storeIds = BuildStoreAssignments(random);

        var sales = BuildSales(random, products, customers, storeIds, discounts, quantities, returns, createdByUserId, approverUserId);
        ScaleProductPrices(products, sales);
        RecalculateSaleTotals(products, sales);
        var inventory = BuildInventory(random, sales);

        return new SeedPlan(stores, suppliers, products, customers, sales, inventory);
    }

    private static List<Store> BuildStores() =>
    [
        new() { StoreId = "S001", StoreName = "Lisbon Flagship", Region = "Lisbon", StoreSizeM2 = 179, Channel = StoreChannel.Physical },
        new() { StoreId = "S002", StoreName = "Porto Center", Region = "Porto", StoreSizeM2 = 728, Channel = StoreChannel.Physical },
        new() { StoreId = "S003", StoreName = "Faro Outlet", Region = "Algarve", StoreSizeM2 = 336, Channel = StoreChannel.Physical },
        new() { StoreId = "S004", StoreName = "Online", Region = "Online", StoreSizeM2 = 950, Channel = StoreChannel.Online },
        new() { StoreId = "S005", StoreName = "Coimbra Boutique", Region = "Coimbra", StoreSizeM2 = 238, Channel = StoreChannel.Physical },
    ];

    private static List<Supplier> BuildSuppliers()
        => SupplierCodes.Select(code => new Supplier { Code = code, Name = code.ToUpperInvariant() }).ToList();

    private static List<ProductSeed> BuildProducts(Random random)
    {
        var products = new List<ProductSeed>(ProductCount);
        for (var i = 1; i <= ProductCount; i++)
        {
            var category = CategorySequence[(i - 1) % CategorySequence.Length];
            var color = ColorPalette[(i - 1) % ColorPalette.Length];
            var size = SizePalette[random.Next(SizePalette.Length)];
            var season = SeasonPalette[random.Next(SeasonPalette.Length)];
            var supplierCode = SupplierCodes[(i - 1) % SupplierCodes.Length];

            var listPrice = category switch
            {
                "Accessories" => 18m + (decimal)random.NextDouble() * 55m,
                "Tops" => 32m + (decimal)random.NextDouble() * 74m,
                "Bottoms" => 48m + (decimal)random.NextDouble() * 86m,
                "Shoes" => 62m + (decimal)random.NextDouble() * 102m,
                _ => 58m + (decimal)random.NextDouble() * 108m,
            };

            var costRatio = category switch
            {
                "Accessories" => 0.24m + (decimal)random.NextDouble() * 0.16m,
                "Tops" => 0.25m + (decimal)random.NextDouble() * 0.16m,
                "Bottoms" => 0.28m + (decimal)random.NextDouble() * 0.16m,
                "Shoes" => 0.30m + (decimal)random.NextDouble() * 0.15m,
                _ => 0.27m + (decimal)random.NextDouble() * 0.16m,
            };

            products.Add(new ProductSeed
            {
                ProductId = $"P{i:000000}",
                Category = category,
                Color = color,
                Size = size,
                Season = season,
                SupplierCode = supplierCode,
                ListPrice = listPrice,
                CostPrice = listPrice * costRatio,
            });
        }

        return products;
    }

    private static List<Customer> BuildCustomers(Random random)
    {
        var customers = new List<Customer>(CustomerCount);
        for (var i = 1; i <= CustomerCount; i++)
        {
            customers.Add(new Customer
            {
                CustomerId = $"C{i:000000}",
                Age = random.Next(18, 76),
                Gender = PickGender(random),
                City = CityPalette[random.Next(CityPalette.Length)],
                Email = $"customer{i:000000}@retail.local",
            });
        }

        return customers;
    }

    private static Gender PickGender(Random random)
    {
        var roll = random.Next(100);
        if (roll < 47)
            return Gender.Female;
        if (roll < 94)
            return Gender.Male;
        if (roll < 98)
            return Gender.Other;
        return Gender.Unspecified;
    }

    private static List<decimal> BuildDiscounts(Random random)
    {
        var values = new List<decimal>(SaleCount);
        values.AddRange(Enumerable.Repeat(0.00m, 27_385));
        values.AddRange(Enumerable.Repeat(0.10m, 9_182));
        values.AddRange(Enumerable.Repeat(0.20m, 4_590));
        values.AddRange(Enumerable.Repeat(0.30m, 2_332));
        Shuffle(values, random);
        return values;
    }

    private static List<int> BuildQuantities(Random random)
    {
        var values = new List<int>(SaleCount);
        values.AddRange(Enumerable.Repeat(1, 7_000));
        values.AddRange(Enumerable.Repeat(2, 11_974));
        values.AddRange(Enumerable.Repeat(3, 20_000));
        values.AddRange(Enumerable.Repeat(4, 4_515));
        Shuffle(values, random);
        return values;
    }

    private static List<bool> BuildReturns(Random random)
    {
        var values = new List<bool>(SaleCount);
        values.AddRange(Enumerable.Repeat(true, ReturnCount));
        values.AddRange(Enumerable.Repeat(false, SaleCount - ReturnCount));
        Shuffle(values, random);
        return values;
    }

    private static List<string> BuildStoreAssignments(Random random)
    {
        var storeIds = new List<string>(SaleCount);
        storeIds.AddRange(Enumerable.Repeat("S001", 8_730));
        storeIds.AddRange(Enumerable.Repeat("S002", 8_680));
        storeIds.AddRange(Enumerable.Repeat("S003", 8_740));
        storeIds.AddRange(Enumerable.Repeat("S004", 8_640));
        storeIds.AddRange(Enumerable.Repeat("S005", 8_699));
        Shuffle(storeIds, random);
        return storeIds;
    }

    private static List<Sale> BuildSales(
        Random random,
        IReadOnlyList<ProductSeed> products,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<string> storeIds,
        IReadOnlyList<decimal> discounts,
        IReadOnlyList<int> quantities,
        IReadOnlyList<bool> returns,
        Guid createdByUserId,
        Guid approverUserId)
    {
        var sales = new List<Sale>(SaleCount);
        for (var i = 0; i < SaleCount; i++)
        {
            var productIndex = PickProductIndex(random);
            var customerIndex = PickCustomerIndex(random);
            var transactionId = $"T{i + 1:0000000}";
            var date = i switch
            {
                0 => StartDate,
                1 => EndDate,
                _ => StartDate.AddDays(random.Next(0, EndDate.DayNumber - StartDate.DayNumber + 1)),
            };

            var productId = products[productIndex].ProductId;
            var quantity = quantities[i];
            var discount = discounts[i];
            var isReturned = returns[i];
            var reason = ReturnReasonPalette[random.Next(ReturnReasonPalette.Length)];
            var item = new SaleItem
            {
                ProductId = productId,
                Quantity = quantity,
                Discount = discount,
            };

            var sale = new Sale
            {
                TransactionId = transactionId,
                Date = date,
                StoreId = storeIds[i],
                CustomerId = customers[customerIndex].CustomerId,
                CreatedByUserId = createdByUserId,
                Status = isReturned ? SaleStatus.Returned : SaleStatus.Completed,
                Items = [item],
            };

            if (isReturned)
            {
                sale.Return = new Return
                {
                    TransactionId = transactionId,
                    Date = date,
                    Reason = reason,
                    Status = ReturnStatus.Approved,
                    ApprovedByUserId = approverUserId,
                    ApprovedAt = DateTime.UtcNow,
                };
            }

            sales.Add(sale);
        }

        return sales;
    }

    private static int PickProductIndex(Random random)
    {
        var roll = random.NextDouble();
        return roll switch
        {
            < 0.60 => random.Next(0, 10_000),
            < 0.85 => random.Next(10_000, 25_000),
            < 0.97 => random.Next(25_000, 40_000),
            _ => random.Next(40_000, ProductCount),
        };
    }

    private static int PickCustomerIndex(Random random)
    {
        var roll = random.NextDouble();
        return roll switch
        {
            < 0.55 => random.Next(0, 5_000),
            < 0.85 => random.Next(5_000, 15_000),
            _ => random.Next(15_000, CustomerCount),
        };
    }

    private static void ScaleProductPrices(IReadOnlyList<ProductSeed> products, IReadOnlyList<Sale> sales)
    {
        var productLookup = products.ToDictionary(p => p.ProductId, StringComparer.Ordinal);

        decimal revenueBase = 0m;
        decimal costBase = 0m;
        foreach (var sale in sales)
        {
            var item = sale.Items[0];
            var product = productLookup[item.ProductId];
            revenueBase += item.Quantity * product.ListPrice * (1m - item.Discount);
            costBase += item.Quantity * product.CostPrice;
        }

        var priceScale = TargetRevenue / revenueBase;
        var costScale = (TargetRevenue - TargetMargin) / costBase;

        foreach (var product in products)
        {
            product.ListPrice = decimal.Round(product.ListPrice * priceScale, 2, MidpointRounding.AwayFromZero);
            product.CostPrice = decimal.Round(product.CostPrice * costScale, 2, MidpointRounding.AwayFromZero);

            var maxCost = decimal.Round(product.ListPrice * 0.68m, 2, MidpointRounding.AwayFromZero);
            if (product.CostPrice > maxCost)
                product.CostPrice = maxCost;
        }
    }

    private static void RecalculateSaleTotals(IReadOnlyList<ProductSeed> products, IReadOnlyList<Sale> sales)
    {
        var productLookup = products.ToDictionary(p => p.ProductId, StringComparer.Ordinal);
        foreach (var sale in sales)
        {
            var item = sale.Items[0];
            var product = productLookup[item.ProductId];
            item.UnitListPrice = product.ListPrice;
            item.UnitCostPrice = product.CostPrice;
            item.Revenue = decimal.Round(item.Quantity * product.ListPrice * (1m - item.Discount), 2, MidpointRounding.AwayFromZero);
            item.Margin = decimal.Round(item.Quantity * ((product.ListPrice * (1m - item.Discount)) - product.CostPrice), 2, MidpointRounding.AwayFromZero);
            sale.RecalculateTotals();
        }
    }

    private static List<InventoryItem> BuildInventory(Random random, IReadOnlyList<Sale> sales)
    {
        var inventory = new List<InventoryItem>();
        foreach (var group in sales
                     .SelectMany(s => s.Items.Select(i => new { s.StoreId, i.ProductId, i.Quantity }))
                     .GroupBy(x => new { x.StoreId, x.ProductId }))
        {
            var soldQuantity = group.Sum(x => x.Quantity);
            var buffer = random.Next(6, 28);
            inventory.Add(new InventoryItem
            {
                StoreId = group.Key.StoreId,
                ProductId = group.Key.ProductId,
                StockOnHand = Math.Max(buffer, (soldQuantity / 3) + buffer),
                ReservedStock = 0,
            });
        }

        return inventory;
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        foreach (var name in new[] { RoleNames.Superadmin, RoleNames.Vendedor, RoleNames.Visualizador })
        {
            if (!await _roles.RoleExistsAsync(name))
                await _roles.CreateAsync(new IdentityRole<Guid>(name));
        }
    }

    private async Task<SeededUsers> SeedUsersAsync(CancellationToken ct)
    {
        var superadmin = await EnsureUserAsync("superadmin@retail.local", "Superadmin#2026", "System Superadmin", Role.Superadmin, ct);
        var seller = await EnsureUserAsync("vendedor@retail.local", "Vendedor#2026", "Seller Demo", Role.Vendedor, ct);
        await EnsureUserAsync("viewer@retail.local", "Viewer#2026", "BI Viewer", Role.Visualizador, ct);
        return new SeededUsers(superadmin.Id, seller.Id);
    }

    private async Task<User> EnsureUserAsync(string email, string password, string fullName, Role role, CancellationToken ct)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existing is not null)
            return existing;

        var user = new User
        {
            FullName = fullName,
            Email = email,
            UserName = email,
            Role = role,
            IsActive = true,
        };

        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not seed user '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");

        await _users.AddToRoleAsync(user, RoleNames.Name(role));
        return user;
    }

    private async Task SeedDiscountPolicyAsync(CancellationToken ct)
    {
        if (await _db.DiscountPolicies.AnyAsync(ct))
            return;

        _db.DiscountPolicies.Add(new DiscountPolicy
        {
            Name = "Default policy",
            MaxDiscount = 0.30m,
            MinMarginPercent = 0.10m,
            RequiresSuperadminApproval = true,
            IsActive = true,
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSuppliersAsync(IReadOnlyList<Supplier> suppliers, CancellationToken ct)
    {
        var existingCodes = (await _db.Suppliers.AsNoTracking().Select(s => s.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toInsert = suppliers.Where(s => !existingCodes.Contains(s.Code)).ToList();
        if (toInsert.Count == 0)
            return;

        _db.Suppliers.AddRange(toInsert);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedStoresAsync(IReadOnlyList<Store> stores, CancellationToken ct)
    {
        var existingIds = (await _db.Stores.AsNoTracking().Select(s => s.StoreId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var toInsert = stores.Where(s => !existingIds.Contains(s.StoreId)).ToList();
        if (toInsert.Count == 0)
            return;

        _db.Stores.AddRange(toInsert);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedProductsAsync(IReadOnlyList<ProductSeed> products, CancellationToken ct)
    {
        var supplierIds = (await _db.Suppliers.AsNoTracking().ToListAsync(ct))
            .ToDictionary(s => s.Code, s => s.Id, StringComparer.OrdinalIgnoreCase);
        var existingIds = (await _db.Products.AsNoTracking().Select(p => p.ProductId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var toInsert = products
            .Where(p => !existingIds.Contains(p.ProductId))
            .Select(p => new Product
            {
                ProductId = p.ProductId,
                Category = p.Category,
                Color = p.Color,
                Size = p.Size,
                Season = p.Season,
                SupplierId = supplierIds[p.SupplierCode],
                CostPrice = p.CostPrice,
                ListPrice = p.ListPrice,
                IsActive = true,
            })
            .ToList();

        await InsertInBatchesAsync(toInsert, ProductCount, ct);
    }

    private async Task SeedCustomersAsync(IReadOnlyList<Customer> customers, CancellationToken ct)
    {
        var existingIds = (await _db.Customers.AsNoTracking().Select(c => c.CustomerId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var toInsert = customers.Where(c => !existingIds.Contains(c.CustomerId)).ToList();
        await InsertInBatchesAsync(toInsert, CustomerCount, ct);
    }

    private async Task SeedSalesAsync(IReadOnlyList<Sale> sales, CancellationToken ct)
    {
        var existingIds = (await _db.Sales.AsNoTracking().Select(s => s.TransactionId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var toInsert = sales.Where(s => !existingIds.Contains(s.TransactionId)).ToList();
        await InsertInBatchesAsync(toInsert, SaleCount, ct);
    }

    private async Task SeedInventoryAsync(IReadOnlyList<InventoryItem> inventory, CancellationToken ct)
    {
        var existingPairs = (await _db.InventoryItems.AsNoTracking()
                .Select(i => new InventoryKey(i.StoreId, i.ProductId))
                .ToListAsync(ct))
            .ToHashSet();

        var toInsert = inventory
            .Where(i => !existingPairs.Contains(new InventoryKey(i.StoreId, i.ProductId)))
            .ToList();

        await InsertInBatchesAsync(toInsert, inventory.Count, ct);
    }

    private async Task InsertInBatchesAsync<T>(IReadOnlyList<T> rows, int expectedCount, CancellationToken ct)
        where T : class
    {
        if (rows.Count == 0)
        {
            _log.LogInformation("Skipping {EntityType}: already seeded.", typeof(T).Name);
            return;
        }

        var priorAutoDetect = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var offset = 0; offset < rows.Count; offset += BatchSize)
            {
                var batch = rows.Skip(offset).Take(BatchSize).ToList();
                _db.AddRange(batch);
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = priorAutoDetect;
        }

        _log.LogInformation("Inserted {Inserted} {EntityType} rows toward expected profile {Expected}.", rows.Count, typeof(T).Name, expectedCount);
    }

    private async Task<long?> TryGetDatabaseSizeAsync(CancellationToken ct)
    {
        try
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(ct);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_database_size(current_database())";
                var result = await command.ExecuteScalarAsync(ct);
                return result switch
                {
                    long value => value,
                    int value => value,
                    decimal value => (long)value,
                    _ => null,
                };
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Could not read PostgreSQL database size.");
            return null;
        }
    }

    private static string FormatBytes(long? value)
    {
        if (value is null)
            return "unknown";

        var bytes = (double)value.Value;
        var units = new[] { "B", "KB", "MB", "GB" };
        var unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        return $"{bytes:0.##}{units[unit]}";
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private sealed record ProductSeed
    {
        public required string ProductId { get; init; }
        public required string Category { get; init; }
        public required string Color { get; init; }
        public required string Size { get; init; }
        public required string Season { get; init; }
        public required string SupplierCode { get; init; }
        public decimal CostPrice { get; set; }
        public decimal ListPrice { get; set; }
    }

    private sealed record SeedPlan(
        IReadOnlyList<Store> Stores,
        IReadOnlyList<Supplier> Suppliers,
        IReadOnlyList<ProductSeed> Products,
        IReadOnlyList<Customer> Customers,
        IReadOnlyList<Sale> Sales,
        IReadOnlyList<InventoryItem> Inventory);

    private readonly record struct SeededUsers(Guid SuperadminId, Guid SellerId);
    private readonly record struct InventoryKey(string StoreId, string ProductId);
}
