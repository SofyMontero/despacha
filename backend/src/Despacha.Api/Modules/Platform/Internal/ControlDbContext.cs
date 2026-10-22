using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>
/// Base de control por ambiente (D-03). En 4A solo contiene empresas y sus bases;
/// usuarios, membresías, roles y permisos llegan en 4B.
/// </summary>
internal sealed class ControlDbContext(DbContextOptions<ControlDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<TenantDatabase>(entity =>
        {
            entity.ToTable("tenant_databases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Server).HasMaxLength(255).IsRequired();
            entity.Property(x => x.DatabaseName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SecretName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantDatabase>(x => x.TenantId);
        });
    }
}

internal enum TenantStatus
{
    Active,
    Suspended,
}

internal sealed class Tenant
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Active;
}

/// <summary>Una por empresa en cada ambiente. Solo guarda el nombre del secreto, nunca credenciales (D-06).</summary>
internal sealed class TenantDatabase
{
    public long Id { get; set; }

    public long TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string Server { get; set; } = string.Empty;

    public int Port { get; set; } = 3306;

    public string DatabaseName { get; set; } = string.Empty;

    public string SecretName { get; set; } = string.Empty;
}
