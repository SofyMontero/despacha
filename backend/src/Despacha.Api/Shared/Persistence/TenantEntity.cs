using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Despacha.Api.Shared.Persistence;

/// <summary>
/// Fila de la base de la empresa. El id admite los enteros históricos. <c>Version</c> 0 es una fila
/// anterior a la aplicación: la primera edición la pasa a 1 y no le atribuye el alta.
/// </summary>
public abstract class TenantEntity
{
    public const string HistoricalOrigin = "historico";

    public const string ApplicationOrigin = "aplicacion";

    public long Id { get; set; }

    public int Version { get; set; }

    public string Origin { get; set; } = ApplicationOrigin;

    public DateTime? CreatedAtUtc { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public long? UpdatedBy { get; set; }

    public void MarkCreated(long? userId, DateTime nowUtc)
    {
        Origin = ApplicationOrigin;
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
        CreatedBy = UpdatedBy = userId;
        Version = 1;
    }

    public void MarkUpdated(long? userId, DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        UpdatedBy = userId;
        Version++;
    }
}

public static class TenantEntityExtensions
{
    public static EntityTypeBuilder<T> ConfigureTenantEntity<T>(this EntityTypeBuilder<T> entity, string table, string idColumn)
        where T : TenantEntity
    {
        entity.ToTable(table);
        entity.HasKey(x => x.Id);
        var id = entity.Property(x => x.Id).HasColumnName(idColumn).ValueGeneratedOnAdd();
        if (entity.Metadata.Model.FindAnnotation("Despacha:Sqlite")?.Value is not true)
        {
            id.HasColumnType("int");
        }
        entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        entity.Property(x => x.Origin).HasColumnName("origen_registro").HasMaxLength(20).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("creado_en").HasColumnType("datetime(6)");
        entity.Property(x => x.CreatedBy).HasColumnName("creado_por").HasColumnType("bigint");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("actualizado_en").HasColumnType("datetime(6)");
        entity.Property(x => x.UpdatedBy).HasColumnName("actualizado_por").HasColumnType("bigint");
        return entity;
    }

    /// <summary>Hace que el UPDATE exija la versión que tenía el cliente; si no coincide, EF lanza un conflicto (409).</summary>
    public static void ExpectVersion<T>(this DbContext db, T entity, int expectedVersion)
        where T : TenantEntity =>
        db.Entry(entity).Property(x => x.Version).OriginalValue = expectedVersion;
}
