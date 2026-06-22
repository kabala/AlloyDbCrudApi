using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<DiscountPolicy> DiscountPolicies => Set<DiscountPolicy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.Role).HasConversion<int>();
            e.HasIndex(u => u.Email).IsUnique();
        });

        b.Entity<IdentityRole<Guid>>().ToTable("roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        b.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        b.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(c => c.CustomerId);
            e.Property(c => c.CustomerId).HasMaxLength(50);
            e.Property(c => c.City).IsRequired().HasMaxLength(120);
            e.Property(c => c.Email).IsRequired().HasMaxLength(256);
            e.Property(c => c.Gender).HasConversion<int>();
            e.Property(c => c.IsActive).HasDefaultValue(true);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasIndex(c => c.Email);
            e.HasMany(c => c.Sales).WithOne(s => s.Customer!).HasForeignKey(s => s.CustomerId);
        });

        b.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(s => s.Id);
            e.Property(s => s.Code).IsRequired().HasMaxLength(50);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(s => s.Code).IsUnique();
            e.HasMany(s => s.Products).WithOne(p => p.Supplier!).HasForeignKey(p => p.SupplierId);
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.ProductId);
            e.Property(p => p.ProductId).HasMaxLength(50);
            e.Property(p => p.Category).IsRequired().HasMaxLength(80);
            e.Property(p => p.Color).IsRequired().HasMaxLength(50);
            e.Property(p => p.Size).HasMaxLength(20);
            e.Property(p => p.Season).HasMaxLength(40);
            e.Property(p => p.CostPrice).HasPrecision(12, 2);
            e.Property(p => p.ListPrice).HasPrecision(12, 2);
            e.Property(p => p.IsActive).HasDefaultValue(true);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasIndex(p => p.Category);
            e.HasIndex(p => p.Season);
            e.HasIndex(p => p.SupplierId);
        });

        b.Entity<Store>(e =>
        {
            e.ToTable("stores");
            e.HasKey(s => s.StoreId);
            e.Property(s => s.StoreId).HasMaxLength(50);
            e.Property(s => s.StoreName).IsRequired().HasMaxLength(200);
            e.Property(s => s.Region).IsRequired().HasMaxLength(120);
            e.Property(s => s.StoreSizeM2).HasDefaultValue(0);
            e.Property(s => s.Channel).HasConversion<int>();
            e.Property(s => s.IsActive).HasDefaultValue(true);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasMany(s => s.Inventory).WithOne(i => i.Store!).HasForeignKey(i => i.StoreId);
            e.HasMany(s => s.Sales).WithOne(s => s.Store!).HasForeignKey(s => s.StoreId);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.ToTable("inventory_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.ProductId).IsRequired().HasMaxLength(50);
            e.Property(i => i.StoreId).IsRequired().HasMaxLength(50);
            e.Property(i => i.StockOnHand).HasDefaultValue(0);
            e.Property(i => i.ReservedStock).HasDefaultValue(0);
            e.Property(i => i.UpdatedAt).HasDefaultValueSql("NOW()");
            e.Property(i => i.RowVersion).IsRowVersion();
            e.HasIndex(i => new { i.StoreId, i.ProductId }).IsUnique();
        });

        b.Entity<Sale>(e =>
        {
            e.ToTable("sales");
            e.HasKey(s => s.TransactionId);
            e.Property(s => s.TransactionId).HasMaxLength(50);
            e.Property(s => s.StoreId).IsRequired().HasMaxLength(50);
            e.Property(s => s.CustomerId).IsRequired().HasMaxLength(50);
            e.Property(s => s.Status).HasConversion<int>();
            e.Property(s => s.TotalRevenue).HasPrecision(14, 2);
            e.Property(s => s.TotalMargin).HasPrecision(14, 2);
            e.Property(s => s.TotalDiscount).HasPrecision(14, 4);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasIndex(s => s.Date);
            e.HasIndex(s => s.StoreId);
            e.HasIndex(s => s.CustomerId);
            e.HasIndex(s => s.Status);
            e.HasMany(s => s.Items).WithOne(i => i.Sale!).HasForeignKey(i => i.TransactionId);
            e.HasOne(s => s.Return).WithOne(r => r.Sale!).HasForeignKey<Return>(r => r.TransactionId);
        });

        b.Entity<SaleItem>(e =>
        {
            e.ToTable("sale_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.TransactionId).IsRequired().HasMaxLength(50);
            e.Property(i => i.ProductId).IsRequired().HasMaxLength(50);
            e.Property(i => i.UnitListPrice).HasPrecision(12, 2);
            e.Property(i => i.UnitCostPrice).HasPrecision(12, 2);
            e.Property(i => i.Revenue).HasPrecision(14, 2);
            e.Property(i => i.Margin).HasPrecision(14, 2);
            e.Property(i => i.Discount).HasPrecision(5, 4);
            e.HasIndex(i => i.TransactionId);
            e.HasIndex(i => i.ProductId);
        });

        b.Entity<Return>(e =>
        {
            e.ToTable("returns");
            e.HasKey(r => r.Id);
            e.Property(r => r.TransactionId).IsRequired().HasMaxLength(50);
            e.Property(r => r.Reason).HasConversion<int>();
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.Notes).HasMaxLength(1000);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasIndex(r => r.TransactionId).IsUnique();
        });

        b.Entity<DiscountPolicy>(e =>
        {
            e.ToTable("discount_policies");
            e.HasKey(d => d.Id);
            e.Property(d => d.Name).IsRequired().HasMaxLength(120);
            e.Property(d => d.MaxDiscount).HasPrecision(5, 4);
            e.Property(d => d.MinMarginPercent).HasPrecision(5, 4);
            e.Property(d => d.IsActive).HasDefaultValue(true);
            e.Property(d => d.CreatedAt).HasDefaultValueSql("NOW()");
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityName).IsRequired().HasMaxLength(80);
            e.Property(a => a.EntityId).IsRequired().HasMaxLength(80);
            e.Property(a => a.Detail).HasMaxLength(2000);
            e.Property(a => a.CorrelationId).HasMaxLength(64);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("NOW()");
            e.HasIndex(a => a.EntityName);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.CreatedAt);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(r => r.Id);
            e.Property(r => r.TokenHash).IsRequired().HasMaxLength(128);
            e.Property(r => r.UserId).IsRequired();
            e.Property(r => r.RevokedAt);
            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => r.UserId);
        });
    }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsExpired && !IsRevoked;
}
