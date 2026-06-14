using AlloyDbCrudApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
