using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Despacha.Api.Modules.Customers.Internal;

/// <summary>
/// Clientes y bares sobre las tablas existentes. Las columnas modernas pertenecen a la
/// migración 5D.20261024.02 y no se dan por existentes.
/// </summary>
internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_customers";

    public DbSet<Establishment> Establishments => Set<Establishment>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.HasAnnotation("Despacha:Sqlite", true);
        }

        modelBuilder.Entity<Establishment>(entity =>
        {
            entity.ConfigureTenantEntity("bares", "id_bar");
            entity.Property(x => x.Name).HasColumnName("nombre_bar").HasMaxLength(CustomerLimits.EstablishmentName).IsRequired();
            entity.Property(x => x.Address).HasColumnName("direccion_bar").HasMaxLength(CustomerLimits.Address);
            entity.Property(x => x.OpenedAtUtc).HasColumnName("fecha_crear_bar").HasColumnType("datetime(6)").NullIfZeroDate();
            entity.Property(x => x.IsActive).HasColumnName("activo");
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ConfigureTenantEntity("clientes", "id_cliente");
            entity.Property(x => x.Zone).HasColumnName("cli_zona").HasMaxLength(CustomerLimits.Zone).IsRequired();
            entity.Property(x => x.NotifyByWhatsApp).HasColumnName("cli_notificar_whatsapp");
            entity.Property(x => x.Name).HasColumnName("cli_nombre").HasMaxLength(CustomerLimits.Name).IsRequired();
            entity.Property(x => x.Phone).HasColumnName("cli_telefono").HasMaxLength(CustomerLimits.Phone).IsRequired();
            entity.Property(x => x.PhoneOriginal).HasColumnName("telefono_original").HasMaxLength(CustomerLimits.Phone);
            entity.Property(x => x.Address).HasColumnName("cli_direccion").HasMaxLength(CustomerLimits.Address).IsRequired();
            entity.Property(x => x.RegisteredAtUtc).HasColumnName("cli_fecha_registro").HasColumnType("datetime(6)").NullIfZeroDate();
            entity.Property(x => x.EstablishmentId).HasColumnName("cli_Bar").HasColumnType("int");
            entity.Property(x => x.LegacyIdentification).HasColumnName("cli_identificacion");
            entity.Property(x => x.Identification).HasColumnName("identificacion_texto").HasMaxLength(CustomerLimits.Identification);
            entity.Property(x => x.CompanyId).HasColumnName("id_empresa").HasColumnType("int");
            entity.Property(x => x.IsActive).HasColumnName("activo");
            entity.HasIndex(x => x.Phone);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Identification);
            entity.HasIndex(x => x.Zone);
            entity.HasIndex(x => x.EstablishmentId);
            entity.HasOne(x => x.Establishment).WithMany().HasForeignKey(x => x.EstablishmentId).IsRequired(false).OnDelete(DeleteBehavior.ClientNoAction);
        });
    }
}

internal static class CustomerLimits
{
    public const int Name = 150;
    public const int EstablishmentName = 150;
    public const int Identification = 30;
    public const int Phone = 20;
    public const int Address = 255;
    public const int Zone = 60;
}

/// <summary>Tabla <c>bares</c>. <c>fecha_crear_bar</c> se conserva; <c>creado_en</c> es la auditoría nueva.</summary>
internal sealed class Establishment : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public DateTime? OpenedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Tabla <c>clientes</c>. El teléfono histórico se devuelve como está. La identificación 0
/// no se presenta como documento. La fecha cero se lee como null.
/// </summary>
internal sealed class Customer : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Identification { get; set; }

    public int LegacyIdentification { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? PhoneOriginal { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Zone { get; set; } = string.Empty;

    public long? EstablishmentId { get; set; }

    public Establishment? Establishment { get; set; }

    public DateTime? RegisteredAtUtc { get; set; }

    public int NotifyByWhatsApp { get; set; } = 1;

    public int? CompanyId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ShownIdentification =>
        Identification == "" ? null
        : !string.IsNullOrWhiteSpace(Identification) ? Identification
        : LegacyIdentification > 0 ? LegacyIdentification.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : null;
}

internal sealed class CustomersDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args) =>
        new(TenantSchemaServiceCollectionExtensions.CreateOptions<CustomersDbContext>(TenantSchema.DesignTimeConnection, CustomersDbContext.MigrationsHistoryTable));
}
