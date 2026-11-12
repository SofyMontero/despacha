using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.EntityFrameworkCore;

namespace Despacha.UnitTests;

/// <summary>
/// Modelo de control sobre las tablas existentes (users, roles, empresas) y su script SQL,
/// sin abrir ninguna conexión. La prueba contra MariaDB real está en las pruebas de integración.
/// </summary>
public class MySqlProviderCompatibilityTests
{
    private static readonly string[] HistoricalTables = ["users", "roles", "empresas"];

    private static ControlDbContext CreateContext() => new ControlDbContextDesignTimeFactory().CreateDbContext([]);

    internal static string ScriptPath() =>
        Path.Combine(RepositoryRoot(), "database", "proposed", "01_identidad_sobre_tablas_existentes_NO_EJECUTAR.sql");

    private static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database", "proposed")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la carpeta database/proposed.");
    }

    private static string ExecutableSql() =>
        string.Join('\n', File.ReadAllLines(ScriptPath()).Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

    [Fact]
    public void El_modelo_usa_las_tablas_y_los_tipos_de_llave_existentes()
    {
        using var context = CreateContext();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("MySql", context.Database.ProviderName);
        Assert.Contains("CREATE TABLE `users`", script);
        Assert.Contains("`id` int NOT NULL AUTO_INCREMENT", script);
        Assert.Contains("CREATE TABLE `empresas`", script);
        Assert.Contains("`id_empresa` int unsigned NOT NULL AUTO_INCREMENT", script);
        Assert.Contains("CREATE TABLE `roles`", script);
        Assert.Contains("`id_rol` int NOT NULL AUTO_INCREMENT", script);
        Assert.Contains("`UserId` int NOT NULL", script);
        Assert.Contains("`TenantId` int unsigned NOT NULL", script);
        Assert.DoesNotContain("CREATE TABLE `tenants`", script);
    }

    [Fact]
    public void No_hay_migraciones_de_control_para_las_bases_existentes()
    {
        using var context = CreateContext();

        Assert.Empty(context.Database.GetMigrations());
    }

    [Fact]
    public void El_script_01_crea_o_agrega_todas_las_columnas_del_modelo()
    {
        using var context = CreateContext();
        var sql = ExecutableSql();
        var missing = new List<string>();

        foreach (var table in context.Model.GetRelationalModel().Tables)
        {
            var available = HistoricalTables.Contains(table.Name)
                ? HistoricalColumns(sql, table.Name).Concat(AddedColumns(sql, table.Name))
                : CreatedColumns(sql, table.Name);
            var set = available.ToHashSet(StringComparer.OrdinalIgnoreCase);
            missing.AddRange(table.Columns.Where(column => !set.Contains(column.Name)).Select(column => $"{table.Name}.{column.Name}"));
        }

        Assert.True(missing.Count == 0, "Columnas del modelo que el script 01 no crea: " + string.Join(", ", missing));
    }

    [Fact]
    public void El_script_01_no_toca_claves_tokens_ni_datos_historicos()
    {
        var sql = ExecutableSql();

        Assert.Contains("despacha.migration_version: " + ControlDbContext.ControlSchemaVersion, File.ReadAllText(ScriptPath()));
        Assert.DoesNotContain("api_token", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(new Regex(@"\b(DROP|RENAME|TRUNCATE)\b|(?<!ON )\bDELETE\b", RegexOptions.IgnoreCase), sql);
        Assert.DoesNotMatch(new Regex(@"SET\s+password\s*=", RegexOptions.IgnoreCase), sql);
        Assert.DoesNotMatch(new Regex(@"UPDATE\s+users\s+SET\s+(direccion|rol|id_rol|email|telefono|nombre|usuario)\s*=", RegexOptions.IgnoreCase), sql);
        Assert.Contains("WHERE u.id_rol = 11", sql);
    }

    /// <summary>Columnas que el paso 1 exige que ya existan.</summary>
    private static IEnumerable<string> HistoricalColumns(string sql, string table) =>
        Regex.Matches(sql, $@"TABLE_NAME = '{table}' AND COLUMN_NAME = '(\w+)'").Select(m => m.Groups[1].Value);

    private static IEnumerable<string> AddedColumns(string sql, string table) =>
        Regex.Matches(sql, $@"ALTER TABLE {table}\b(.*?);", RegexOptions.Singleline)
            .SelectMany(alter => Regex.Matches(alter.Groups[1].Value, @"ADD COLUMN IF NOT EXISTS (\w+)").Select(m => m.Groups[1].Value));

    private static IEnumerable<string> CreatedColumns(string sql, string table)
    {
        var match = Regex.Match(sql, $@"CREATE TABLE IF NOT EXISTS {table} \((.*?)\n\) ENGINE", RegexOptions.Singleline);
        return match.Success
            ? match.Groups[1].Value.Split('\n').Select(line => line.Trim().Split(' ')[0]).Where(name => name.Length > 0)
            : [];
    }
}
