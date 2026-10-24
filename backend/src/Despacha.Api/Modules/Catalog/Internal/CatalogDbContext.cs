using System.Globalization;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Despacha.Api.Modules.Catalog.Internal;

/// <summary>
/// Catálogo sobre las tablas existentes. Las columnas de auditoría, versión y <c>activo</c>
/// pertenecen a la migración 5D.20261024.02: el guardia las exige antes de abrir la base
/// y las migraciones EF de <c>catalog_*</c> no se aplican sobre ella.
/// </summary>
internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_catalog";

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Promotion> Promotions => Set<Promotion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.HasAnnotation("Despacha:Sqlite", true);
        }

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ConfigureTenantEntity("categorias", "id_categoria");
            entity.Property(x => x.Name).HasColumnName("nombre_categoria").HasMaxLength(CatalogLimits.CategoryName).IsRequired();
            entity.Property(x => x.ImageKey).HasColumnName("imagen_categoria").HasMaxLength(CatalogLimits.ImageKey).EmptyImageAsNull();
            entity.Property(x => x.IsActive).HasColumnName("activo");
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ConfigureTenantEntity("productos", "id_producto");
            entity.Property(x => x.Code).HasColumnName("codigo_productos").HasColumnType("int").IsRequired()
                .HasConversion(value => int.Parse(value, CultureInfo.InvariantCulture), value => value.ToString(CultureInfo.InvariantCulture));
            entity.Property(x => x.Name).HasColumnName("descripcion_producto").HasMaxLength(CatalogLimits.ProductName).IsRequired();
            entity.Property(x => x.CategoryId).HasColumnName("id_cate_producto").HasColumnType("int");
            entity.Property(x => x.UnitPrice).HasColumnName("precio_unidad_producto").HasPrecision(12, 2);
            entity.Property(x => x.PackPrice).HasColumnName("precio_paca_producto").HasPrecision(12, 2);
            entity.Property(x => x.UnitsPerPack).HasColumnName("cantidad_paca_producto");
            entity.Property(x => x.SellsByUnit).HasColumnName("acti_Unidad").HasConversion<int>();
            entity.Property(x => x.LegacyStatus).HasColumnName("estado_producto").HasMaxLength(5).IsRequired();
            entity.Property(x => x.SalesCount).HasColumnName("cantidad_venta");
            entity.Property(x => x.IsActive).HasColumnName("activo");
            entity.Property(x => x.ImageKey).HasColumnName("imagen_producto").HasMaxLength(CatalogLimits.ImageKey).EmptyImageAsNull();
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).IsRequired(false).OnDelete(DeleteBehavior.ClientNoAction);
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.ConfigureTenantEntity("promociones", "id_promocion");
            entity.Property(x => x.Title).HasColumnName("titulo").HasMaxLength(CatalogLimits.PromotionTitle).IsRequired();
            entity.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(CatalogLimits.PromotionDescription);
            entity.Property(x => x.Code).HasColumnName("codigo").HasColumnType("int").IsRequired()
                .HasConversion(value => int.Parse(value, CultureInfo.InvariantCulture), value => value.ToString(CultureInfo.InvariantCulture));
            entity.Property(x => x.ProductId).HasColumnName("producto_id").HasColumnType("int");
            entity.Property(x => x.UnitPrice).HasColumnName("precio_unidad_producto").HasPrecision(12, 2);
            entity.Property(x => x.PackPrice).HasColumnName("precio_paca_producto").HasPrecision(12, 2);
            entity.Property(x => x.SellsByUnit).HasColumnName("acti_Unidad").HasConversion<int>();
            entity.Property(x => x.Priority).HasColumnName("prioridad");
            entity.Property(x => x.IsActive).HasColumnName("estado");
            entity.Property(x => x.ImageKey).HasColumnName("imagen").HasMaxLength(CatalogLimits.ImageKey);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.ProductId);
            entity.HasIndex(x => new { x.IsActive, x.Priority });
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).IsRequired(false).OnDelete(DeleteBehavior.ClientNoAction);
        });
    }
}

internal static class CatalogLimits
{
    public const int Code = 50;
    public const int CategoryName = 100;
    public const int ProductName = 250;
    public const int PromotionTitle = 150;
    public const int PromotionDescription = 1000;
    public const int ImageKey = 300;
    public const decimal MaxPrice = 9_999_999_999.99m;
}

internal interface IHasImage
{
    string? ImageKey { get; set; }
}

/// <summary>Tabla <c>categorias</c>. <c>activo</c> no existe hasta la migración 5D.20261024.02.</summary>
internal sealed class Category : TenantEntity, IHasImage
{
    public string Name { get; set; } = string.Empty;

    public string? ImageKey { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Tabla <c>productos</c>. El código es entero y no es único. <c>cantidad_venta</c> se conserva
/// y las ediciones no la recalculan. La categoría 19, ausente en TEST, se lee y no se anula.
/// </summary>
internal sealed class Product : TenantEntity, IHasImage
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long? CategoryId { get; set; }

    public Category? Category { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal PackPrice { get; set; }

    public int UnitsPerPack { get; set; } = 1;

    public bool SellsByUnit { get; set; } = true;

    /// <summary><c>estado_producto</c> histórico. Se mantiene alineado con <see cref="IsActive"/> al guardar el estado.</summary>
    public string LegacyStatus { get; set; } = "1";

    public int SalesCount { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ImageKey { get; set; }

    public void UseActive(bool active)
    {
        IsActive = active;
        LegacyStatus = active ? "1" : "0";
    }
}

/// <summary>
/// Tabla <c>promociones</c>, no <c>promos</c>. <c>producto_id</c> queda vacío cuando el código
/// corresponde a más de un producto. La aplicación no lo rellena sola.
/// </summary>
internal sealed class Promotion : TenantEntity, IHasImage
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Code { get; set; } = string.Empty;

    public long? ProductId { get; set; }

    public Product? Product { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal PackPrice { get; set; }

    public bool SellsByUnit { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ImageKey { get; set; }
}

internal sealed class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(TenantSchemaServiceCollectionExtensions.CreateOptions<CatalogDbContext>(TenantSchema.DesignTimeConnection, CatalogDbContext.MigrationsHistoryTable));
}
