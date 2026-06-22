using System.Globalization;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlloyDbCrudApi.Infrastructure.Seeding;

public class RetailSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ILogger<RetailSeeder> _log;
    private readonly IConfiguration _cfg;

    public RetailSeeder(AppDbContext db, UserManager<User> users, RoleManager<IdentityRole<Guid>> roles, ILogger<RetailSeeder> log, IConfiguration cfg)
    {
        _db = db; _users = users; _roles = roles; _log = log; _cfg = cfg;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        await SeedUsersAsync(ct);
        await SeedDiscountPolicyAsync(ct);
        await SeedCatalogAsync(ct);
    }

    private string DatasetsPath => _cfg["Seed:DatasetsPath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "BI", "Tarea_01_BI", "datasets", "Retail_Store");

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        foreach (var name in new[] { RoleNames.Superadmin, RoleNames.Vendedor, RoleNames.Visualizador })
        {
            if (!await _roles.RoleExistsAsync(name))
                await _roles.CreateAsync(new IdentityRole<Guid>(name));
        }
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        await EnsureUserAsync("superadmin@retail.local", "Superadmin#2026", "System Superadmin", Role.Superadmin, ct);
        await EnsureUserAsync("vendedor@retail.local", "Vendedor#2026", "Seller Demo", Role.Vendedor, ct);
        await EnsureUserAsync("viewer@retail.local", "Viewer#2026", "BI Viewer", Role.Visualizador, ct);
    }

    private async Task EnsureUserAsync(string email, string pwd, string full, Role role, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email, ct)) return;
        var user = new User { FullName = full, Email = email, UserName = email, Role = role, IsActive = true };
        var res = await _users.CreateAsync(user, pwd);
        if (!res.Succeeded)
        {
            _log.LogWarning("Could not seed user {Email}: {Errors}", email, string.Join("; ", res.Errors.Select(e => e.Description)));
            return;
        }
        await _users.AddToRoleAsync(user, RoleNames.Name(role));
        _log.LogInformation("Seeded user {Email} as {Role}", email, role);
    }

    private async Task SeedDiscountPolicyAsync(CancellationToken ct)
    {
        if (await _db.DiscountPolicies.AnyAsync(ct)) return;
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

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        if (await _db.Stores.AnyAsync(ct))
        {
            _log.LogInformation("Catalog already seeded, skipping CSV import.");
            return;
        }

        var suppliers = ReadSuppliers();
        foreach (var s in suppliers.Values)
        {
            if (!await _db.Suppliers.AnyAsync(x => x.Code == s.Code, ct))
                _db.Suppliers.Add(s);
        }
        await _db.SaveChangesAsync(ct);
        var supplierByCode = await _db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Code, ct);

        var stores = ReadStores();
        foreach (var st in stores) _db.Stores.Add(st);
        await _db.SaveChangesAsync(ct);

        var products = ReadProducts(supplierByCode);
        var validProducts = new List<Product>();
        var rejected = 0;
        foreach (var p in products)
        {
            try { p.Validate(); validProducts.Add(p); }
            catch (InvalidProductException) { rejected++; }
        }
        _db.Products.AddRange(validProducts);
        await _db.SaveChangesAsync(ct);
        if (rejected > 0) _log.LogWarning("Rejected {Count} products due to invalid data.", rejected);

        var customers = ReadCustomers();
        _db.Customers.AddRange(customers);
        await _db.SaveChangesAsync(ct);

        var productById = validProducts.ToDictionary(p => p.ProductId);
        var customerIds = new HashSet<string>(customers.Select(c => c.CustomerId));
        var storeIds = new HashSet<string>(stores.Select(s => s.StoreId));
        var sales = ReadSales(productById, customerIds, storeIds);
        _db.Sales.AddRange(sales);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Seeded {Stores} stores, {Suppliers} suppliers, {Products} products, {Customers} customers, {Sales} sales.",
            stores.Count, suppliers.Count, validProducts.Count, customers.Count, sales.Count);
    }

    private Dictionary<string, Supplier> ReadSuppliers()
    {
        var path = Path.Combine(DatasetsPath, "product_data.csv");
        var byCode = new Dictionary<string, Supplier>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return byCode;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length < 6) continue;
            var code = f[5].Trim();
            if (string.IsNullOrWhiteSpace(code) || byCode.ContainsKey(code)) continue;
            byCode[code] = new Supplier { Code = code, Name = code };
        }
        return byCode;
    }

    private List<Store> ReadStores()
    {
        var path = Path.Combine(DatasetsPath, "store_data.csv");
        var list = new List<Store>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length < 4) continue;
            var name = f[1].Trim();
            var isOnline = name.Contains("Online", StringComparison.OrdinalIgnoreCase) || f[2].Trim().Equals("Online", StringComparison.OrdinalIgnoreCase);
            list.Add(new Store
            {
                StoreId = f[0].Trim(),
                StoreName = name,
                Region = f[2].Trim(),
                StoreSizeM2 = int.TryParse(f[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var m2) ? m2 : 0,
                Channel = isOnline ? StoreChannel.Online : StoreChannel.Physical,
            });
        }
        return list;
    }

    private List<Product> ReadProducts(IReadOnlyDictionary<string, Supplier> suppliers)
    {
        var path = Path.Combine(DatasetsPath, "product_data.csv");
        var list = new List<Product>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length < 8) continue;
            var supplierCode = f[5].Trim();
            list.Add(new Product
            {
                ProductId = f[0].Trim(),
                Category = f[1].Trim(),
                Color = string.IsNullOrWhiteSpace(f[2].Trim()) ? "Unspecified" : f[2].Trim(),
                Size = f[3].Trim(),
                Season = f[4].Trim(),
                SupplierId = suppliers.TryGetValue(supplierCode, out var sup) ? sup.Id : null,
                CostPrice = decimal.TryParse(f[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var cp) ? cp : 0m,
                ListPrice = decimal.TryParse(f[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var lp) ? lp : 0m,
            });
        }
        return list;
    }

    private List<Customer> ReadCustomers()
    {
        var path = Path.Combine(DatasetsPath, "customer_data.csv");
        var list = new List<Customer>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length < 5) continue;
            list.Add(new Customer
            {
                CustomerId = f[0].Trim(),
                Age = int.TryParse(f[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : 0,
                Gender = GenderNames.Parse(f[2]),
                City = f[3].Trim(),
                Email = string.IsNullOrWhiteSpace(f[4].Trim()) ? "unknown@local" : f[4].Trim(),
            });
        }
        return list;
    }

    private List<Sale> ReadSales(IReadOnlyDictionary<string, Product> products, IReadOnlySet<string> customerIds, IReadOnlySet<string> storeIds)
    {
        var path = Path.Combine(DatasetsPath, "sales_data.csv");
        var list = new List<Sale>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length < 8) continue;
            var tid = f[0].Trim();
            var dateStr = f[1].Trim();
            if (!DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            var productId = f[2].Trim();
            var storeId = f[3].Trim();
            var customerId = f[4].Trim();
            if (!products.ContainsKey(productId)) continue;
            if (!customerIds.Contains(customerId)) continue;
            if (!storeIds.Contains(storeId)) continue;
            var quantity = int.TryParse(f[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) ? qty : 0;
            if (quantity <= 0) continue;
            var discount = decimal.TryParse(f[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            var returned = f[7].Trim() == "1";

            var product = products[productId];
            var revenue = quantity * product.ListPrice * (1m - discount);
            var margin = quantity * ((product.ListPrice * (1m - discount)) - product.CostPrice);

            var sale = new Sale
            {
                TransactionId = tid,
                Date = date,
                StoreId = storeId,
                CustomerId = customerId,
                Status = returned ? SaleStatus.Returned : SaleStatus.Completed,
                TotalRevenue = revenue,
                TotalMargin = margin,
                TotalDiscount = discount * quantity,
                TotalQuantity = quantity,
                CreatedByUserId = Guid.Empty,
                Items = new List<SaleItem>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        Quantity = quantity,
                        Discount = discount,
                        UnitListPrice = product.ListPrice,
                        UnitCostPrice = product.CostPrice,
                        Revenue = revenue,
                        Margin = margin,
                    },
                },
            };
            if (returned)
            {
                sale.Return = new Return
                {
                    TransactionId = tid,
                    Date = date,
                    Reason = ReturnReason.Other,
                    Status = ReturnStatus.Approved,
                    ApprovedByUserId = Guid.Empty,
                    ApprovedAt = DateTime.UtcNow,
                };
            }
            list.Add(sale);
        }
        return list;
    }
}
