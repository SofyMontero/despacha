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
