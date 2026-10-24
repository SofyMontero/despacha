using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Shared.Persistence;

/// <summary>
/// Esquema de un módulo dentro de la base de cada empresa (D-07). El Migrator recorre los esquemas registrados;
/// la API nunca migra al arrancar.
/// </summary>
public sealed class TenantSchema(string module, string migrationsHistoryTable, Func<IServiceProvider, string, string, DbContext> factory)
{
    /// <summary>Conexión ficticia para generar scripts sin conectarse a ninguna base.</summary>
    public const string DesignTimeConnection = "Server=localhost;Database=despacha_tenant_design;User ID=design;";

    public string Module { get; } = module;

    public string MigrationsHistoryTable { get; } = migrationsHistoryTable;

    public DbContext CreateContext(IServiceProvider services, string connectionString) =>
        factory(services, connectionString, MigrationsHistoryTable);
}

public static class TenantSchemaServiceCollectionExtensions
{
    public static IServiceCollection AddTenantSchema<TContext>(this IServiceCollection services, string module, string migrationsHistoryTable)
        where TContext : DbContext
    {
        services.AddSingleton(new TenantSchema(module, migrationsHistoryTable, (sp, connectionString, history) =>
            ActivatorUtilities.CreateInstance<TContext>(sp, CreateOptions<TContext>(connectionString, history))));
        return services;
    }

    public static DbContextOptions<TContext> CreateOptions<TContext>(string connectionString, string migrationsHistoryTable)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDespachaMySql(connectionString, migrationsHistoryTable);
        return builder.Options;
    }
}
