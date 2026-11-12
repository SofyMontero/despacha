using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>
/// Base de control por ambiente (D-03): empresas, sus bases, usuarios del personal, membresías,
/// roles, permisos, módulos activos, sesiones de renovación y auditoría de plataforma.
/// Los roles de Identity no se usan: los roles son por empresa.
/// <c>users</c>, <c>roles</c> y <c>empresas</c> son las tablas históricas, ampliadas por
/// <c>database/proposed/01_identidad_sobre_tablas_existentes_NO_EJECUTAR.sql</c>; las migraciones
/// de EF de control solo sirven para bases nuevas y vacías.
/// </summary>
internal sealed class ControlDbContext(DbContextOptions<ControlDbContext> options) : IdentityUserContext<StaffUser, long>(options)
{
    public const string ControlSchemaVersion = "5D.20261026.01";

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FillRequiredLegacyColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        FillRequiredLegacyColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary><c>users.email</c> y <c>users.telefono</c> son NOT NULL sin valor por defecto en la tabla histórica.</summary>
    private void FillRequiredLegacyColumns()
    {
        foreach (var entry in ChangeTracker.Entries<StaffUser>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.Email ??= string.Empty;
                entry.Entity.PhoneNumber ??= string.Empty;
            }
        }
    }

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

        // En SQLite (pruebas) un id autoincremental debe ser INTEGER: los tipos MySQL solo se aplican fuera de SQLite.
        var sqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        string? UserKey() => sqlite ? null : "int";
        string? TenantKey() => sqlite ? null : "int unsigned";
        string? RoleKey() => sqlite ? null : "int";

        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType(UserKey());
            entity.Property(x => x.UserName).HasColumnName("usuario").HasMaxLength(StaffUser.LoginMaxLength);
            entity.Property(x => x.NormalizedUserName).HasColumnName("usuario_normalizado").HasMaxLength(StaffUser.LoginMaxLength);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasColumnName("email_normalizado").HasMaxLength(100);
            entity.Property(x => x.EmailConfirmed).HasColumnName("email_confirmado");
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.SecurityStamp).HasColumnName("sello_seguridad");
            entity.Property(x => x.ConcurrencyStamp).HasColumnName("sello_concurrencia");
            entity.Property(x => x.PhoneNumber).HasColumnName("telefono").HasMaxLength(15).IsRequired();
            entity.Property(x => x.PhoneNumberConfirmed).HasColumnName("telefono_confirmado");
            entity.Property(x => x.TwoFactorEnabled).HasColumnName("doble_factor");
            // MySql.EntityFrameworkCore no guarda DateTimeOffset en MariaDB (queda 0001-01-01): se guarda en UTC.
            entity.Property(x => x.LockoutEnd).HasColumnName("bloqueo_hasta").HasConversion(new ValueConverter<DateTimeOffset, DateTime>(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc))));
            entity.Property(x => x.LockoutEnabled).HasColumnName("bloqueo_habilitado");
            entity.Property(x => x.AccessFailedCount).HasColumnName("intentos_fallidos");
            entity.Property(x => x.DisplayName).HasColumnName("nombre").HasMaxLength(StaffUser.DisplayNameMaxLength).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("activo");
            entity.Property(x => x.RequiresPasswordChange).HasColumnName("requiere_cambio_clave");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("creado_en").HasColumnType(sqlite ? null : "datetime(6)");            entity.Property(x => x.LegacyPassword).HasColumnName("password").HasMaxLength(255).IsRequired();
        });
        modelBuilder.Entity<IdentityUserClaim<long>>(entity =>
        {
            entity.ToTable("user_claims");
            entity.Property(x => x.UserId).HasColumnType(UserKey());
        });
        modelBuilder.Entity<IdentityUserLogin<long>>(entity =>
        {
            entity.ToTable("user_logins");
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.ProviderKey).HasMaxLength(128);
            entity.Property(x => x.UserId).HasColumnType(UserKey());
        });
        modelBuilder.Entity<IdentityUserToken<long>>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.Property(x => x.LoginProvider).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.UserId).HasColumnType(UserKey());
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("empresas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_empresa").HasColumnType(TenantKey());
            entity.Property(x => x.Code).HasColumnName("slug").HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_empresas_slug");
            entity.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasColumnName("activa")
                .HasConversion(v => v == TenantStatus.Active, v => v ? TenantStatus.Active : TenantStatus.Suspended);
        });

        modelBuilder.Entity<TenantDatabase>(entity =>
        {
            entity.ToTable("tenant_databases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantId).HasColumnType(TenantKey());
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
            entity.Property(x => x.TenantId).HasColumnType(TenantKey());
            entity.Property(x => x.Module).HasMaxLength(50);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id_rol").HasColumnType(RoleKey());
            entity.Property(x => x.TenantId).HasColumnName("id_empresa").HasColumnType(TenantKey());
            entity.Property(x => x.Name).HasColumnName("rol_nombre").HasMaxLength(Role.NameMaxLength).IsRequired();
            entity.Property(x => x.IsTemplate).HasColumnName("es_plantilla");
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasDatabaseName("uq_roles_empresa_nombre");
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.Permission });
            entity.Property(x => x.RoleId).HasColumnType(RoleKey());
            entity.Property(x => x.Permission).HasMaxLength(100);
            entity.HasOne<Role>().WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasColumnType(UserKey());
            entity.Property(x => x.TenantId).HasColumnType(TenantKey());
            entity.Property(x => x.RoleId).HasColumnType(RoleKey());
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
            entity.Property(x => x.UserId).HasColumnType(UserKey());
            entity.Property(x => x.TenantId).HasColumnType(TenantKey());
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
    /// <summary>Largos de <c>users.usuario</c> y <c>users.nombre</c> en la tabla histórica.</summary>
    public const int LoginMaxLength = 50;

    public const int DisplayNameMaxLength = 50;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool RequiresPasswordChange { get; set; }

    /// <summary>Null en los usuarios históricos: la tabla no guardaba la fecha de alta.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    /// <summary>
    /// Columna histórica <c>password</c>. Solo se lee para aceptar una vez un hash bcrypt y
    /// reemplazarlo por el de Identity; los usuarios nuevos la dejan vacía.
    /// </summary>
    public string LegacyPassword { get; set; } = string.Empty;
}

internal sealed class TenantModule
{
    public long TenantId { get; set; }

    public string Module { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

internal sealed class Role
{
    /// <summary>Largo de <c>roles.rol_nombre</c> en la tabla histórica.</summary>
    public const int NameMaxLength = 20;

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
