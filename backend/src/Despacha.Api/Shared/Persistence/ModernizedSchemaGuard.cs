using MySql.Data.MySqlClient;

namespace Despacha.Api.Shared.Persistence;

/// <summary>
/// Comprueba, sin escribir, que la base ya tiene las columnas de la migración
/// <c>5D.20261024.02</c>. Si faltan, la API no abre el contexto y no las crea.
/// </summary>
public static class ModernizedSchemaGuard
{
    public const string MigrationVersion = "5D.20261024.02";

    public static readonly ColumnRequirement[] Requirements =
    [
        Column("categorias", "activo"),
        Column("categorias", "origen_registro"),
        Column("categorias", "version"),
        Column("categorias", "creado_en"),
        Column("categorias", "creado_por"),
        Column("categorias", "actualizado_en"),
        Column("categorias", "actualizado_por"),
        Column("categorias", "nombre_categoria", minLength: 100),
        Column("categorias", "imagen_categoria", minLength: 300),
        Column("productos", "activo"),
        Column("productos", "origen_registro"),
        Column("productos", "version"),
        Column("productos", "creado_en"),
        Column("productos", "creado_por"),
        Column("productos", "actualizado_en"),
        Column("productos", "actualizado_por"),
        Column("productos", "precio_unidad_producto", dataType: "decimal"),
        Column("productos", "precio_paca_producto", dataType: "decimal"),
        Column("productos", "imagen_producto", minLength: 300),
        Column("promociones", "producto_id", nullable: true),
        Column("promociones", "origen_registro"),
        Column("promociones", "version"),
        Column("promociones", "creado_por"),
        Column("promociones", "actualizado_en"),
        Column("promociones", "actualizado_por"),
        Column("promociones", "titulo", minLength: 150),
        Column("promociones", "precio_unidad_producto", dataType: "decimal"),
        Column("promociones", "precio_paca_producto", dataType: "decimal"),
        Column("bares", "activo"),
        Column("bares", "origen_registro"),
        Column("bares", "version"),
        Column("bares", "creado_en"),
        Column("bares", "creado_por"),
        Column("bares", "actualizado_en"),
        Column("bares", "actualizado_por"),
        Column("bares", "nombre_bar", minLength: 150),
        Column("bares", "direccion_bar", minLength: 255),
        Column("clientes", "activo"),
        Column("clientes", "origen_registro"),
        Column("clientes", "version"),
        Column("clientes", "creado_en"),
        Column("clientes", "creado_por"),
        Column("clientes", "actualizado_en"),
        Column("clientes", "actualizado_por"),
        Column("clientes", "telefono_original"),
        Column("clientes", "identificacion_texto"),
        Column("clientes", "cli_telefono", minLength: 20),
        Column("clientes", "cli_nombre", minLength: 150),
        Column("clientes", "cli_direccion", minLength: 255),
        Column("clientes", "cli_zona", minLength: 60),
        Column("clientes", "cli_Bar", nullable: true),
        Column("clientes", "cli_fecha_registro", nullable: true),
    ];

    private static readonly HashSet<string> Ready = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Gate = new();

    /// <summary>Las pruebas unitarias sustituyen la lectura de information_schema. En producción queda en null.</summary>
    internal static Func<string, CancellationToken, Task>? TestProbe { get; set; }

    public static async Task EnsureReadyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (TestProbe is not null)
        {
            await TestProbe(connectionString, cancellationToken);
            return;
        }

        await EnsureAsync(connectionString, MigrationVersion, Requirements, cancellationToken);
    }

    /// <summary>Igual que <see cref="EnsureReadyAsync"/>, para cualquier versión y lista de columnas (por ejemplo, la base de control).</summary>
    public static async Task EnsureAsync(
        string connectionString,
        string version,
        IReadOnlyCollection<ColumnRequirement> requirements,
        CancellationToken cancellationToken = default)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new DatabaseConnectionBlockedException("La cadena de conexión no define la base.");
        }

        var schema = builder.Database;
        var key = $"{version}|{builder.Server}:{builder.Port}/{schema}";
        lock (Gate)
        {
            if (Ready.Contains(key))
            {
                return;
            }
        }

        var tables = requirements.Select(r => r.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = new List<string>();
        for (var i = 0; i < tables.Length; i++)
        {
            names.Add($"@t{i}");
            command.Parameters.AddWithValue($"@t{i}", tables[i]);
        }

        command.CommandText = $"""
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME IN ({string.Join(", ", names)})
            """;
        command.Parameters.AddWithValue("@schema", schema);

        var found = new Dictionary<string, ColumnFact>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var fact = new ColumnFact(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    string.Equals(reader.GetString(4), "YES", StringComparison.OrdinalIgnoreCase),
                    !reader.IsDBNull(5) && !string.Equals(reader.GetString(5), "NULL", StringComparison.OrdinalIgnoreCase));
                found[$"{reader.GetString(0)}.{fact.Name}"] = fact;
            }
        }

        var missing = requirements.Where(requirement => !Matches(requirement, found)).Select(requirement => requirement.Label).ToArray();
        if (missing.Length > 0)
        {
            var shown = missing.Length > 12 ? [.. missing.Take(12), $"y {missing.Length - 12} más"] : missing;
            throw new DatabaseConnectionBlockedException(
                $"La base '{schema}' no tiene el esquema {version}. Falta: {string.Join(", ", shown)}. No se modificó la base.");
        }

        lock (Gate)
        {
            Ready.Add(key);
        }
    }

    public static ColumnRequirement Column(
        string table,
        string column,
        string? dataType = null,
        int? minLength = null,
        bool? nullable = null,
        bool requiresDefault = false,
        bool onlyIfPresent = false) =>
        new(table, column, dataType, minLength, nullable, requiresDefault, onlyIfPresent);

    private readonly record struct ColumnFact(string Name, string DataType, long? Length, bool Nullable, bool HasDefault);

    public readonly record struct ColumnRequirement(
        string Table,
        string Column,
        string? DataType,
        int? MinLength,
        bool? Nullable,
        bool RequiresDefault = false,
        bool OnlyIfPresent = false)
    {
        public string Label => RequiresDefault ? $"{Table}.{Column} (valor por defecto)" : $"{Table}.{Column}";
    }

    private static bool Matches(ColumnRequirement requirement, IReadOnlyDictionary<string, ColumnFact> found)
    {
        if (!found.TryGetValue($"{requirement.Table}.{requirement.Column}", out var fact))
        {
            return requirement.OnlyIfPresent;
        }

        if (requirement.DataType is not null && !string.Equals(fact.DataType, requirement.DataType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requirement.MinLength is int length && (fact.Length is null || fact.Length < length))
        {
            return false;
        }

        if (requirement.RequiresDefault && !fact.HasDefault)
        {
            return false;
        }

        return requirement.Nullable is not bool nullable || fact.Nullable == nullable;
    }
}
