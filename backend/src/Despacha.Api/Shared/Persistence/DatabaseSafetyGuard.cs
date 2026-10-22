using Despacha.Api.Shared.Runtime;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace Despacha.Api.Shared.Persistence;

public sealed class DatabaseSafetyOptions
{
    public const string SectionName = "Database";

    /// <summary>Servidores permitidos en el ambiente Test. En Local solo se aceptan direcciones de loopback.</summary>
    public string[] AllowedHosts { get; set; } = [];
}

public sealed class DatabaseConnectionBlockedException(string message) : Exception(message);

/// <summary>
/// Impide conexiones accidentales a bases que no corresponden al ambiente:
/// Local solo usa loopback, Test solo los servidores declarados y ninguno de los dos acepta bases "prod".
/// </summary>
public sealed class DatabaseSafetyGuard(DespachaRuntime runtime, IOptions<DatabaseSafetyOptions> options)
{
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    public void EnsureAllowed(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var hosts = (builder.Server ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (hosts.Length == 0)
        {
            throw new DatabaseConnectionBlockedException("La cadena de conexión no define un servidor.");
        }

        foreach (var host in hosts)
        {
            EnsureAllowed(host, builder.Database ?? string.Empty);
        }
    }

    public void EnsureAllowed(string host, string database)
    {
        if (runtime.Environment == DespachaEnvironment.Prod)
        {
            if (ContainsAny(database, "test", "local"))
            {
                throw new DatabaseConnectionBlockedException($"La base '{database}' no corresponde al ambiente Prod.");
            }

            return;
        }

        var allowedHosts = runtime.IsLocal ? LoopbackHosts : options.Value.AllowedHosts;
        if (!allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            throw new DatabaseConnectionBlockedException($"El servidor '{host}' no está permitido en el ambiente {runtime.Environment}.");
        }

        if (ContainsAny(database, "prod"))
        {
            throw new DatabaseConnectionBlockedException($"La base '{database}' parece de producción y está bloqueada en el ambiente {runtime.Environment}.");
        }
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
