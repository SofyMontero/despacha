using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Orders.Internal;

/// <summary>
/// Pedidos sobre las tablas existentes. No agrega columnas: la base histórica ya las tiene
/// y una base local vacía las crea con <c>tenant-apply</c>.
/// </summary>
internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_orders";

    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();

    public DbSet<OrderLine> Lines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var sqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        string? Key() => sqlite ? null : "int";

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("pedidos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_pedido").HasColumnType(Key()).ValueGeneratedOnAdd();
            entity.Property(x => x.CustomerId).HasColumnName("ped_cliente").HasColumnType(Key());
            entity.Property(x => x.Status).HasColumnName("ped_estado").HasMaxLength(10).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("ped_fecha").HasColumnType(sqlite ? null : "datetime");
            entity.Property(x => x.Number).HasColumnName("ped_numfac").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Total).HasColumnName("ped_total").HasPrecision(12, 2);
            entity.Property(x => x.Phone).HasColumnName("ped_numCliente").HasMaxLength(20);
            entity.Property(x => x.Note).HasColumnName("ped_observacion").HasColumnType(sqlite ? "TEXT" : "text");
            entity.Property(x => x.Address).HasColumnName("ped_sede").HasMaxLength(255);
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("detalle_pedidos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_detalle").HasColumnType(Key()).ValueGeneratedOnAdd();
            entity.Property(x => x.OrderId).HasColumnName("id_pedido").HasColumnType(Key());
            entity.Property(x => x.ProductId).HasColumnName("id_producto").HasColumnType(Key());
            entity.Property(x => x.Name).HasColumnName("nombre_producto").HasMaxLength(250).IsRequired();
            entity.Property(x => x.Kind).HasColumnName("tipo_producto").HasMaxLength(10).IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("cantidad");
            entity.Property(x => x.UnitPrice).HasColumnName("precio_unitario").HasPrecision(12, 2);
            entity.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            entity.HasIndex(x => x.OrderId);
            entity.HasOne<CustomerOrder>().WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class CustomerOrder
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string Status { get; set; } = "pendiente";

    public DateTime CreatedAt { get; set; }

    public string Number { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string? Phone { get; set; }

    public string? Note { get; set; }

    public string? Address { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}

internal sealed class OrderLine
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long? ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = "unidad";

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }
}
