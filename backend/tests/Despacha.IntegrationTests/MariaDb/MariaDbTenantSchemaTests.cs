using Despacha.Api.Modules.Catalog.Internal;
using Despacha.Api.Modules.Customers.Internal;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Despacha.IntegrationTests.MariaDb;

/// <summary>Migraciones de la base de empresa (Fase 5) sobre MariaDB 11.8. Mismas condiciones que <see cref="MariaDbCompatibilityTests"/>.</summary>
[Collection(MariaDbCollection.Name)]
public class MariaDbTenantSchemaTests(MariaDbServer server)
{
    [MariaDbFact]
    public async Task Las_migraciones_de_empresa_se_aplican_dos_veces_sin_cambios()
    {
        var connection = await server.CreateDatabaseAsync();

        await LegacySchemaGuard.EnsureNotLegacyAsync(connection);
        await using (var catalog = Catalog(connection))
        await using (var customers = Customers(connection))
        {
            await catalog.Database.MigrateAsync();
            await customers.Database.MigrateAsync();
            await catalog.Database.MigrateAsync();
            Assert.Empty(await catalog.Database.GetPendingMigrationsAsync());
            Assert.Empty(await customers.Database.GetPendingMigrationsAsync());
        }

        var tables = Convert.ToInt32(await MariaDbServer.ScalarAsync(connection,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND ENGINE = 'InnoDB'"));
        Assert.Equal(7, tables);
    }

    [MariaDbFact]
    public async Task Precios_texto_y_busqueda_se_comportan_como_se_espera()
    {
        var connection = await MigratedAsync();
        const string name = "Aguardiente Peñalosa ✓ 🚚";
        await using (var db = Catalog(connection))
        {
            var category = new Category { Name = "Licores", IsActive = true };
            category.MarkCreated(1, DateTime.UtcNow);
            db.Categories.Add(category);
            var product = new Product { Code = "101", Name = name, Category = category, UnitPrice = 9_999_999_999.99m, PackPrice = 0.01m, IsActive = true };
            product.MarkCreated(1, DateTime.UtcNow);
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using var check = Catalog(connection);
        var saved = await check.Products.SingleAsync(p => p.Name.Contains("aguardiente"));
        Assert.Equal(name, saved.Name);
        Assert.Equal(9_999_999_999.99m, saved.UnitPrice);
        Assert.Equal(0.01m, saved.PackPrice);
    }

    [MariaDbFact]
    public async Task La_version_desactualizada_falla_por_concurrencia()
    {
        var connection = await MigratedAsync();
        long id;
        await using (var db = Customers(connection))
        {
            var establishment = new Establishment { Name = "Bar", IsActive = true };
            establishment.MarkCreated(1, DateTime.UtcNow);
            db.Establishments.Add(establishment);
            await db.SaveChangesAsync();
            id = establishment.Id;
        }

        await using (var first = Customers(connection))
        {
            var row = await first.Establishments.SingleAsync(e => e.Id == id);
            first.ExpectVersion(row, 1);
            row.Name = "Primero";
            row.MarkUpdated(1, DateTime.UtcNow);
            await first.SaveChangesAsync();
        }

        await using var second = Customers(connection);
        var stale = await second.Establishments.SingleAsync(e => e.Id == id);
        second.ExpectVersion(stale, 1);
        stale.Name = "Segundo";
        stale.MarkUpdated(1, DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [MariaDbFact]
    public async Task No_migra_una_base_con_tablas_del_sistema_heredado()
    {
        var connection = await server.CreateDatabaseAsync();
        await MariaDbServer.ExecuteAsync(connection, "CREATE TABLE productos (id_productos INT PRIMARY KEY)");

        await Assert.ThrowsAsync<DatabaseConnectionBlockedException>(() => LegacySchemaGuard.EnsureNotLegacyAsync(connection));
    }

    private async Task<string> MigratedAsync()
    {
        var connection = await server.CreateDatabaseAsync();
        await using var catalog = Catalog(connection);
        await using var customers = Customers(connection);
        catalog.GetService<IRelationalDatabaseCreator>().CreateTables();
        customers.GetService<IRelationalDatabaseCreator>().CreateTables();
        return connection;
    }

    private static CatalogDbContext Catalog(string connection) =>
        new(TenantSchemaServiceCollectionExtensions.CreateOptions<CatalogDbContext>(connection, CatalogDbContext.MigrationsHistoryTable));

    private static CustomersDbContext Customers(string connection) =>
        new(TenantSchemaServiceCollectionExtensions.CreateOptions<CustomersDbContext>(connection, CustomersDbContext.MigrationsHistoryTable));
}
