using MySql.Data.MySqlClient;

namespace Despacha.Api.Shared.Persistence;

/// <summary>
/// Impide migrar una base que tenga tablas del PHP heredado: las bases de empresa son nuevas y la base
/// heredada de Hostinger nunca se modifica. Solo lee <c>information_schema</c>.
/// </summary>
public static class LegacySchemaGuard
{
    public static readonly IReadOnlyList<string> LegacyTables =
        ["pedidos", "detalle_pedidos", "productos", "promociones", "clientes", "bares", "categorias", "users"];

    public static async Task EnsureNotLegacyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(connectionString, cancellationToken);
        if (found.Count > 0)
        {
            var database = new MySqlConnectionStringBuilder(connectionString).Database;
            throw new DatabaseConnectionBlockedException(
                $"La base '{database}' tiene tablas del sistema heredado ({string.Join(", ", found)}). No se migra.");
        }
    }

    /// <summary>Nombres de tablas heredadas presentes. No escribe.</summary>
    public static async Task<IReadOnlyList<string>> FindAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new DatabaseConnectionBlockedException("La cadena de conexión no define la base de la empresa.");
        }

        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = @schema";
        command.Parameters.AddWithValue("@schema", database);

        var found = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.GetString(0);
                if (LegacyTables.Contains(table, StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(table);
                }
            }
        }

        return found;
    }
}
