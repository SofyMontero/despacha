using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace Despacha.Api.Shared.Persistence;

/// <summary>Único punto donde se elige el proveedor MySQL de EF Core (ver backend/README.md).</summary>
public static class DespachaMySql
{
    public static DbContextOptionsBuilder UseDespachaMySql(this DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySQL(WithZeroDates(connectionString));

    /// <summary>Variante para las bases de empresa: cada módulo lleva su propio historial de migraciones.</summary>
    public static DbContextOptionsBuilder UseDespachaMySql(this DbContextOptionsBuilder builder, string connectionString, string migrationsHistoryTable) =>
        builder.UseMySQL(WithZeroDates(connectionString), options => options.MigrationsHistoryTable(migrationsHistoryTable));

    /// <summary>Lee una fecha cero como DateTime.MinValue, sin escribir en la base.</summary>
    private static string WithZeroDates(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowZeroDateTime = true,
            ConvertZeroDateTime = true,
        };
        return builder.ConnectionString;
    }
}
