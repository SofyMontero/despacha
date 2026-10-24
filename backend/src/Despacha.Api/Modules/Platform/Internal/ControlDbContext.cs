using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>
/// Base de control por ambiente (D-03): empresas, sus bases, usuarios del personal, membresías,
/// roles, permisos, módulos activos, sesiones de renovación y auditoría de plataforma.
/// Los roles de Identity no se usan: los roles son por empresa.
/// </summary>
internal sealed class ControlDbContext(DbContextOptions<ControlDbContext> options) : IdentityUserContext<StaffUser, long>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantDatabase> TenantDatabases => Set<TenantDatabase>();

    public DbSet<TenantModule> TenantModules => Set<TenantModule>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PlatformAuditEvent> AuditEvents => Set<PlatformAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.LegacyUserId);
            entity.HasIndex(x => x.LegacyUserId).IsUnique();
        });
        modelBuilder.Entity<IdentityUserClaim<long>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<long>>(entity =>
        {
            entity.ToTable("user_logins");
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.ProviderKey).HasMaxLength(128);
        });
        modelBuilder.Entity<IdentityUserToken<long>>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(128);
        });

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

        modelBuilder.Entity<TenantModule>(entity =>
        {
            entity.ToTable("tenant_modules");
            entity.HasKey(x => new { x.TenantId, x.Module });
            entity.Property(x => x.Module).HasMaxLength(50);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.Permission });
            entity.Property(x => x.Permission).HasMaxLength(100);
            entity.HasOne<Role>().WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.FamilyId);
            entity.Property(x => x.RevokedReason).HasMaxLength(30);
            entity.HasOne<StaffUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformAuditEvent>(entity =>
        {
            entity.ToTable("platform_audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(200);
            entity.HasIndex(x => x.OccurredAtUtc);
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

/// <summary>Usuario del personal (administrador, vendedor, domiciliario). Los clientes no viven aquí (D-09).</summary>
internal sealed class StaffUser : IdentityUser<long>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool RequiresPasswordChange { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Id de <c>usuarios_legacy</c>. No reemplaza el id de Identity: jornadas y visitas siguen
    /// apuntando a ese id legado, y la membresía solo se crea para un rol verificado.
    /// </summary>
    public long? LegacyUserId { get; set; }
}

internal sealed class TenantModule
{
    public long TenantId { get; set; }

    public string Module { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

internal sealed class Role
{
    public long Id { get; set; }

    public long TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsTemplate { get; set; }

    public List<RolePermission> Permissions { get; set; } = [];
}

internal sealed class RolePermission
{
    public long RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}

internal enum MembershipStatus
{
    Active,
    Suspended,
}

internal sealed class Membership
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long TenantId { get; set; }

    public long RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Solo se guarda el hash SHA-256 del token. La familia permite revocar toda la cadena ante reutilización.</summary>
internal sealed class RefreshToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? TenantId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }
}

/// <summary>Auditoría de plataforma sin datos personales: solo identificadores y la acción.</summary>
internal sealed class PlatformAuditEvent
{
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Action { get; set; } = string.Empty;

    public long? UserId { get; set; }

    public long? TenantId { get; set; }

    public string? Detail { get; set; }
}
