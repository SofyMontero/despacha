using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Testcontainers.MariaDb;

namespace Despacha.IntegrationTests.MariaDb;

/// <summary>
/// Servidor MariaDB 11.8.9 desechable. Pruebas opcionales: no corren en un 'dotnet test' normal.
/// Activar con DESPACHA_MARIADB_TESTS=docker (Testcontainers) o con DESPACHA_MARIADB_TEST_CONNECTION
/// apuntando a una instancia local aislada (loopback) con permiso para crear y borrar bases.
/// Nunca apuntar al esquema TEST de Hostinger.
/// </summary>
public sealed class MariaDbServer : IAsyncLifetime
{
    public const string Image = "mariadb:11.8.9";
    public const string ExpectedVersion = "11.8.9";
    public const string DockerVariable = "DESPACHA_MARIADB_TESTS";
    public const string ConnectionVariable = "DESPACHA_MARIADB_TEST_CONNECTION";

    private readonly List<string> _databases = [];
    private MariaDbContainer? _container;
    private string? _adminConnection;

    public static string? SkipReason =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 }
        || string.Equals(Environment.GetEnvironmentVariable(DockerVariable), "docker", StringComparison.OrdinalIgnoreCase)
            ? null
            : $"MariaDB no configurado: defina {DockerVariable}=docker o {ConnectionVariable}.";

    public async Task InitializeAsync()
    {
        if (SkipReason is not null)
        {
            return;
        }

        _adminConnection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrEmpty(_adminConnection))
        {
            _container = new MariaDbBuilder(Image).WithUsername("root").Build();
            await _container.StartAsync();
            _adminConnection = _container.GetConnectionString();
        }

        var host = new MySqlConnectionStringBuilder(_adminConnection).Server;
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException("Las pruebas MariaDB solo se permiten contra un servidor local aislado.");
        }
    }

    /// <summary>Crea una base vacía con nombre aleatorio; se elimina al terminar.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "despacha_it_" + Guid.NewGuid().ToString("N")[..12];
        await ExecuteAsync(_adminConnection!, $"CREATE DATABASE `{name}`");
        _databases.Add(name);
        return new MySqlConnectionStringBuilder(_adminConnection!) { Database = name }.ConnectionString;
    }

    public static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Contraseña del usuario ficticio 'vendedor', guardada como bcrypt $2y$ igual que el PHP.</summary>
    public const string HistoricalSellerPassword = "Clave-Historica-2026";

    /// <summary>
    /// Estructura de users, roles y empresas copiada del volcado de TEST (sin datos reales) más filas ficticias:
    /// usuario 1 Administrador con clave en texto plano, 2 sin rol con bcrypt y 9 Paseador.
    /// </summary>
    public async Task<string> CreateHistoricalControlDatabaseAsync()
    {
        var connection = await CreateDatabaseAsync();
        var bcrypt = "$2y$" + BCrypt.Net.BCrypt.HashPassword(HistoricalSellerPassword, 4)[4..];
        await ExecuteAsync(connection, $"""
            CREATE TABLE `users` (
              `id` int(11) NOT NULL, `usuario` varchar(50) NOT NULL, `password` varchar(255) NOT NULL,
              `email` varchar(100) NOT NULL, `telefono` varchar(15) NOT NULL, `direccion` varchar(50) NOT NULL,
              `nombre` varchar(50) NOT NULL, `rol` int(2) NOT NULL, `api_token` varchar(64) DEFAULT NULL,
              `id_empresa` int(10) UNSIGNED DEFAULT NULL, `id_rol` int(11) DEFAULT NULL, `ops_api_token` varchar(64) DEFAULT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
            CREATE TABLE `roles` (`id_rol` int(11) NOT NULL, `rol_nombre` varchar(20) NOT NULL, `rol_estado` int(2) NOT NULL)
              ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
            CREATE TABLE `empresas` (
              `id_empresa` int(10) UNSIGNED NOT NULL, `nombre` varchar(120) NOT NULL, `slug` varchar(60) NOT NULL,
              `modelo_negocio` varchar(40) NOT NULL DEFAULT 'bares', `activa` tinyint(1) NOT NULL DEFAULT 1,
              `creado_en` datetime NOT NULL DEFAULT current_timestamp()
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
            ALTER TABLE `empresas` ADD PRIMARY KEY (`id_empresa`), ADD UNIQUE KEY `uq_empresas_slug` (`slug`);
            ALTER TABLE `roles` ADD PRIMARY KEY (`id_rol`);
            ALTER TABLE `users` ADD PRIMARY KEY (`id`), ADD UNIQUE KEY `usuario` (`usuario`), ADD KEY `rol` (`rol`);
            ALTER TABLE `empresas` MODIFY `id_empresa` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
            ALTER TABLE `roles` MODIFY `id_rol` int(11) NOT NULL AUTO_INCREMENT;
            ALTER TABLE `users` MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;
            INSERT INTO empresas (id_empresa, nombre, slug) VALUES (1, 'Empresa ficticia', 'ficticia');
            INSERT INTO roles VALUES (10, 'Domiciliario', 1), (11, 'Administrador', 1), (12, 'Paseador', 1),
              (13, 'Supervisor', 1), (14, 'Vendedor', 1), (15, 'Cajero', 1);
            INSERT INTO users (id, usuario, password, email, telefono, direccion, nombre, rol, api_token, id_empresa, id_rol) VALUES
              (1, 'admin', 'texto-plano', '', '', '', 'Admin ficticio', 1, 'token-ficticio', NULL, 11),
              (2, 'vendedor', '{bcrypt}', 'v@example.com', '3001234567', 'Calle 1', 'Vendedor ficticio', 2, NULL, NULL, NULL),
              (9, 'paseo', 'otra', '', '', '', 'Paseador ficticio', 3, NULL, 1, 12);
            """);
        return connection;
    }

    /// <summary>
    /// Ejecuta database/proposed/01 como lo haría phpMyAdmin: las líneas DELIMITER son del cliente,
    /// y la base autorizada se cambia por la base desechable de la prueba.
    /// </summary>
    public static async Task ApplyIdentityScriptAsync(string connectionString, bool authorizeDatabase = true)
    {
        var database = new MySqlConnectionStringBuilder(connectionString).Database;
        var lines = File.ReadAllLines(IdentityScriptPath())
            .Where(line => !line.StartsWith("DELIMITER", StringComparison.Ordinal))
            .Select(line => line.Replace("END $$", "END;", StringComparison.Ordinal));
        var script = string.Join('\n', lines);
        if (authorizeDatabase)
        {
            script = script.Replace("DEFAULT 'u633742531_Multi_test'", $"DEFAULT '{database}'", StringComparison.Ordinal);
        }

        await ExecuteAsync(connectionString, script);
    }

    private static string IdentityScriptPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database", "proposed")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "database", "proposed", "01_identidad_sobre_tablas_existentes_NO_EJECUTAR.sql");
    }

    internal static ControlDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ControlDbContext>();
        builder.UseDespachaMySql(connectionString);
        return new ControlDbContext(builder.Options);
    }

    public async Task DisposeAsync()
    {
        if (_adminConnection is not null)
        {
            foreach (var database in _databases)
            {
                await ExecuteAsync(_adminConnection, $"DROP DATABASE IF EXISTS `{database}`");
            }
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class MariaDbCollection : ICollectionFixture<MariaDbServer>
{
    public const string Name = "MariaDb";
}

public sealed class MariaDbFactAttribute : FactAttribute
{
    public MariaDbFactAttribute()
    {
        Skip = MariaDbServer.SkipReason;
    }
}
