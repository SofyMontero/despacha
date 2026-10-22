using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Despacha.UnitTests;

/// <summary>
/// Prueba técnica mínima del proveedor MySQL con EF Core 10: construye el modelo y genera el DDL
/// sin abrir ninguna conexión. La prueba contra un servidor MySQL real queda pendiente.
/// </summary>
public class MySqlProviderCompatibilityTests
{
    [Fact]
    public void El_proveedor_MySQL_genera_el_esquema_de_control_sin_conectarse()
    {
        var builder = new DbContextOptionsBuilder<ControlDbContext>();
        builder.UseDespachaMySql("Server=localhost;Database=despacha_control_local;User ID=despacha_local;");

        using var context = new ControlDbContext(builder.Options);
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("MySql", context.Database.ProviderName);
        Assert.Contains("CREATE TABLE `tenants`", script);
        Assert.Contains("CREATE TABLE `tenant_databases`", script);
        Assert.Contains("CREATE UNIQUE INDEX", script);
    }
}
