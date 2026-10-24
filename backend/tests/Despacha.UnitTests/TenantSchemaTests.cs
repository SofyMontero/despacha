using Despacha.Api.Modules.Catalog.Internal;
using Despacha.Api.Modules.Customers.Internal;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Despacha.UnitTests;

/// <summary>
/// El modelo de empresa apunta a las tablas reales. Las migraciones EF antiguas siguen creando
/// catalog_* y no se aplican sobre TEST: el guardia las bloquea y el SQL vive en database/proposed.
/// </summary>
public class TenantSchemaTests
{
    public static TheoryData<string> Modules => new() { "Catalog", "Customers" };

    [Theory]
    [MemberData(nameof(Modules))]
    public void El_modelo_usa_las_tablas_reales_y_la_migracion_ef_antigua_queda_bloqueada(string module)
    {
        using var context = Create(module);
        var tables = context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).ToArray();

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.True(context.Database.HasPendingModelChanges(),
            "El modelo ya coincide con una migración EF. No generes una migración que altere TEST: el cambio de esquema es el SQL 5D.20261024.02.");
        Assert.DoesNotContain(tables, table => table!.StartsWith("catalog_", StringComparison.Ordinal) || table.StartsWith("customers_", StringComparison.Ordinal));

        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE `categorias`", script);
        Assert.DoesNotContain("CREATE TABLE `clientes`", script);
        var prefix = module == "Catalog" ? "catalog_" : "customers_";
        Assert.Contains($"CREATE TABLE `{prefix}", script);
    }

    [Fact]
    public void Las_tablas_mapeadas_son_las_de_negocio()
    {
        using var catalog = Create("Catalog");
        using var customers = Create("Customers");

        Assert.Equal("categorias", catalog.Model.FindEntityType(typeof(Category))!.GetTableName());
        Assert.Equal("productos", catalog.Model.FindEntityType(typeof(Product))!.GetTableName());
        Assert.Equal("promociones", catalog.Model.FindEntityType(typeof(Promotion))!.GetTableName());
        Assert.Equal("bares", customers.Model.FindEntityType(typeof(Establishment))!.GetTableName());
        Assert.Equal("clientes", customers.Model.FindEntityType(typeof(Customer))!.GetTableName());
        Assert.Equal("codigo_productos", catalog.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Code))!.GetColumnName());
        Assert.Equal("producto_id", catalog.Model.FindEntityType(typeof(Promotion))!.FindProperty(nameof(Promotion.ProductId))!.GetColumnName());
        Assert.Equal("activo", catalog.Model.FindEntityType(typeof(Category))!.FindProperty(nameof(Category.IsActive))!.GetColumnName());
        Assert.Contains(ModernizedSchemaGuard.Requirements, requirement => requirement.Label == "promociones.producto_id");
        Assert.Equal("5D.20261024.02", ModernizedSchemaGuard.MigrationVersion);
    }

    [Fact]
    public void El_codigo_de_producto_y_el_telefono_no_tienen_indice_unico_por_los_duplicados_historicos()
    {
        using var catalog = Create("Catalog");
        using var customers = Create("Customers");

        Assert.False(catalog.Model.FindEntityType(typeof(Product))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Product.Code)).IsUnique);
        Assert.False(customers.Model.FindEntityType(typeof(Customer))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Customer.Phone)).IsUnique);
    }

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 500, 1, 100)]
    [InlineData(4, 25, 4, 25)]
    public void La_pagina_se_ajusta_a_limites_seguros(int? page, int? size, int expectedPage, int expectedSize)
    {
        var query = PageQuery.From(page, size);

        Assert.Equal(expectedPage, query.Page);
        Assert.Equal(expectedSize, query.PageSize);
        Assert.Equal((expectedPage - 1) * expectedSize, query.Skip);
    }

    private static DbContext Create(string module) => module switch
    {
        "Catalog" => new CatalogDbContextDesignTimeFactory().CreateDbContext([]),
        "Customers" => new CustomersDbContextDesignTimeFactory().CreateDbContext([]),
        _ => throw new ArgumentOutOfRangeException(nameof(module)),
    };
}
