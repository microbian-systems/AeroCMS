using Aero.Cms.Core;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Core;
using Aero.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aero.Cms.Modules.Commerce.Orders.Data;

public sealed class CommerceDbContext : DbContext
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<Buyer> Buyers => Set<Buyer>();

    public CommerceDbContext(DbContextOptions<CommerceDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Decimal precision for all decimal properties
        foreach (var property in mb.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        mb.Entity<OrderEntity>(e =>
        {
            e.ToTable(Schemas.Tables.Orders);
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.CustomerId).HasMaxLength(100);
            e.Property(x => x.PaymentReference).HasMaxLength(200);
            e.OwnsOne(x => x.ShippingAddress, a =>
            {
                a.Property(p => p.Street).HasColumnName("shipping_street").HasMaxLength(500);
                a.Property(p => p.City).HasColumnName("shipping_city").HasMaxLength(200);
                a.Property(p => p.State).HasColumnName("shipping_state").HasMaxLength(100);
                a.Property(p => p.PostalCode).HasColumnName("shipping_postal_code").HasMaxLength(20);
                a.Property(p => p.Country).HasColumnName("shipping_country").HasMaxLength(100);
            });
            e.OwnsOne(x => x.BillingAddress, a =>
            {
                a.Property(p => p.Street).HasColumnName("billing_street").HasMaxLength(500);
                a.Property(p => p.City).HasColumnName("billing_city").HasMaxLength(200);
                a.Property(p => p.State).HasColumnName("billing_state").HasMaxLength(100);
                a.Property(p => p.PostalCode).HasColumnName("billing_postal_code").HasMaxLength(20);
                a.Property(p => p.Country).HasColumnName("billing_country").HasMaxLength(100);
            });
            e.HasMany(x => x.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<OrderItem>(e =>
        {
            e.ToTable(Schemas.Tables.OrderItems);
            e.HasKey(x => x.Id);
            e.Property(x => x.ProductName).HasMaxLength(500);
            e.Property(x => x.Sku).HasMaxLength(100);
        });

        mb.Entity<Buyer>(e =>
        {
            e.ToTable(Schemas.Tables.Buyers);
            e.HasKey(x => x.Id);
            e.Property(x => x.IdentityId).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.HasIndex(x => x.IdentityId).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        AssignSnowflakeIds();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignSnowflakeIds();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AssignSnowflakeIds()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Added, Entity: IEntity { Id: 0 } entity })
            {
                entity.Id = Snowflake.NewId();
            }
        }
    }
}
