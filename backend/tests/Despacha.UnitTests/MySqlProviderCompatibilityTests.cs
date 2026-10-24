using Despacha.Api.Modules.Platform.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Despacha.UnitTests;

/// <summary>
/// Prueba técnica del proveedor MySQL con EF Core 10 y de las migraciones de control,
/// sin abrir ninguna conexión. La prueba contra un servidor MySQL real queda pendiente.
/// </summary>
public class MySqlProviderCompatibilityTests
{
    private static ControlDbContext CreateContext() => new ControlDbContextDesignTimeFactory().CreateDbContext([]);

    [Fact]
    public void El_proveedor_MySQL_genera_el_esquema_de_control_sin_conectarse()
    {
        using var context = CreateContext();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("MySql", context.Database.ProviderName);
        foreach (var table in new[] { "tenants", "tenant_databases", "users", "memberships", "roles", "role_permissions", "tenant_modules", "refresh_tokens", "platform_audit_events" })
        {
            Assert.Contains($"CREATE TABLE `{table}`", script);
        }

        Assert.Contains("CREATE UNIQUE INDEX", script);
    }

    [Fact]
    public void Las_migraciones_cubren_todo_el_modelo()
    {
        using var context = CreateContext();

        Assert.Contains(context.Database.GetMigrations(), id => id.EndsWith("_InitialControl", StringComparison.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges(), "Hay cambios de modelo sin migración: ejecute 'dotnet ef migrations add'.");
    }

    [Fact]
    public void El_script_de_migracion_es_SQL_MySQL_y_no_guarda_secretos()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE `refresh_tokens`", script);
        Assert.Contains("`TokenHash` char(64)", script);
        Assert.DoesNotContain("BEGIN", script);
        Assert.DoesNotContain("Password=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO `users`", script);
    }
}
