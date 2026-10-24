using System.Text;
using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Despacha.Migrator;

/// <summary>
/// Migraciones de la base de control y de las bases de empresa (D-07). La API nunca migra al arrancar.
/// Comandos:
///   script [--from Migracion] [--output archivo.sql]
///                                   SQL para revisión desde la última migración aplicada; no se conecta.
///   apply --confirm                 Aplica solo las migraciones pendientes (según __EFMigrationsHistory) a ConnectionStrings:Control.
///   provision-tenant --code C --name N --db-server S --db-name D --db-secret K --admin-login L [--admin-name A] [--db-port P]
///                                   Contraseña del administrador: variable DESPACHA_ADMIN_PASSWORD o captura oculta.
///   tenant-script [--module M] [--from Migracion] [--output archivo.sql]
///                                   SQL de los módulos de la base de empresa para revisión; no se conecta.
///   tenant-apply --tenant C --confirm [--module M]
///                                   Migra la base de la empresa C (datos en la base de control y secreto TenantSecrets:*).
///                                   Se niega si la base tiene tablas del PHP heredado.
/// </summary>
internal static class MigratorProgram
{
    public const string AdminPasswordVariable = "DESPACHA_ADMIN_PASSWORD";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: script | apply --confirm | provision-tenant ... | tenant-script | tenant-apply --tenant C --confirm (ver README).");
            return 1;
        }

        var options = MigratorArguments.Parse(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "script" => Script(options),
                "apply" => await ApplyAsync(options),
                "provision-tenant" => await ProvisionTenantAsync(options),
                "tenant-script" => TenantScript(options),
                "tenant-apply" => await TenantApplyAsync(options),
                _ => Fail($"Comando desconocido: {args[0]}"),
            };
        }
        catch (Exception ex) when (ex is OptionsValidationException or InvalidOperationException or ArgumentException
            or DespachaException or DatabaseConnectionBlockedException)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>Esquemas de la base de empresa registrados por los módulos, en orden estable.</summary>
    internal static IReadOnlyList<TenantSchema> TenantSchemas(IServiceProvider services, string? module)
    {
        var schemas = services.GetServices<TenantSchema>().OrderBy(s => s.Module, StringComparer.Ordinal).ToList();
        if (module is null)
        {
            return schemas;
        }

        var selected = schemas.Where(s => string.Equals(s.Module, module, StringComparison.OrdinalIgnoreCase)).ToList();
        return selected.Count > 0
            ? selected
            : throw new ArgumentException($"Módulo sin esquema de empresa: {module}. Disponibles: {string.Join(", ", schemas.Select(s => s.Module))}.");
    }

    /// <summary>Script plano por módulo. Con --from se exige --module, porque cada módulo tiene su propio historial.</summary>
    internal static string GenerateTenantScript(string? module, string? fromMigration)
    {
        if (fromMigration is not null && module is null)
        {
            throw new ArgumentException("--from requiere --module.");
        }

        var services = new ServiceCollection();
        services.AddDespachaModules(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var script = new StringBuilder();
        foreach (var schema in TenantSchemas(provider, module))
        {
            using var context = schema.CreateContext(provider, TenantSchema.DesignTimeConnection);
            script.AppendLine($"-- Módulo {schema.Module} (historial {schema.MigrationsHistoryTable})");
            script.AppendLine(context.GetService<IMigrator>().GenerateScript(fromMigration: fromMigration));
        }

        return script.ToString();
    }

    private static int TenantScript(MigratorArguments options)
    {
        var script = GenerateTenantScript(options.Value("module"), options.Value("from"));
        if (options.Value("output") is { } output)
        {
            File.WriteAllText(output, script, new UTF8Encoding(false));
            Console.WriteLine($"Script escrito en {output}");
        }
        else
        {
            Console.WriteLine(script);
        }

        return 0;
    }

    private static async Task<int> TenantApplyAsync(MigratorArguments options)
    {
        var code = options.Required("tenant");
        if (!options.Flag("confirm"))
        {
            return Fail("tenant-apply exige --confirm. Revise antes el script generado con 'tenant-script'.");
        }

        using var host = BuildHost();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenantId = await services.GetRequiredService<ControlDbContext>().Tenants.AsNoTracking()
            .Where(t => t.Code == code)
            .Select(t => (long?)t.Id)
            .SingleOrDefaultAsync()
            ?? throw new ArgumentException($"La empresa '{code}' no existe en la base de control.");

        var connectionString = await services.GetRequiredService<TenantConnectionProvider>().GetConnectionStringAsync(tenantId, CancellationToken.None);
        await LegacySchemaGuard.EnsureNotLegacyAsync(connectionString);

        Console.WriteLine($"Ambiente {services.GetRequiredService<DespachaRuntime>().Environment}; empresa '{code}'.");
        foreach (var schema in TenantSchemas(services, options.Value("module")))
        {
            await using var context = schema.CreateContext(services, connectionString);
            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
            Console.WriteLine($"Módulo {schema.Module}: migraciones pendientes {pending.Count}");
            await context.Database.MigrateAsync();
        }

        Console.WriteLine("Migraciones de la empresa aplicadas.");
        return 0;
    }

    /// <summary>
    /// Script plano desde la migración indicada. El modo idempotente de MySql.EntityFrameworkCore emite
    /// sintaxis de SQL Server (IF ... BEGIN ... END), inválida en MySQL; la idempotencia la da 'apply'.
    /// </summary>
    internal static string GenerateScript(string? fromMigration)
    {
        using var context = new ControlDbContextDesignTimeFactory().CreateDbContext([]);
        return context.GetService<IMigrator>().GenerateScript(fromMigration: fromMigration);
    }

    private static int Script(MigratorArguments options)
    {
        var script = GenerateScript(options.Value("from"));
        if (options.Value("output") is { } output)
        {
            File.WriteAllText(output, script, new UTF8Encoding(false));
            Console.WriteLine($"Script escrito en {output}");
        }
        else
        {
            Console.WriteLine(script);
        }

        return 0;
    }

    private static async Task<int> ApplyAsync(MigratorArguments options)
    {
        if (!options.Flag("confirm"))
        {
            return Fail("apply exige --confirm. Revise antes el script generado con 'script'.");
        }

        using var host = BuildHost();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Console.WriteLine($"Ambiente {scope.ServiceProvider.GetRequiredService<DespachaRuntime>().Environment}; migraciones pendientes: {pending.Count}");
        await db.Database.MigrateAsync();
        Console.WriteLine("Migraciones aplicadas.");
        return 0;
    }

    private static async Task<int> ProvisionTenantAsync(MigratorArguments options)
    {
        var password = Environment.GetEnvironmentVariable(AdminPasswordVariable) ?? ReadHidden("Contraseña del administrador: ");
        var request = new TenantProvisioningRequest(
            options.Required("code"),
            options.Required("name"),
            options.Required("db-server"),
            int.Parse(options.Value("db-port") ?? "3306", System.Globalization.CultureInfo.InvariantCulture),
            options.Required("db-name"),
            options.Required("db-secret"),
            options.Required("admin-login"),
            options.Value("admin-name") ?? options.Required("admin-login"),
            password);

        using var host = BuildHost();
        await using var scope = host.Services.CreateAsyncScope();
        var tenantId = await scope.ServiceProvider.GetRequiredService<TenantProvisioningService>().ProvisionAsync(request);
        Console.WriteLine($"Empresa '{request.Code}' creada con id {tenantId}.");
        return 0;
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            ApplicationName = typeof(MigratorProgram).Assembly.GetName().Name,
            EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Development,
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddDespachaRuntime();
        builder.Services.AddDespachaModules(builder.Configuration);

        var host = builder.Build();
        _ = host.Services.GetRequiredService<IOptions<ControlDatabaseOptions>>().Value;
        return host;
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace && value.Length > 0)
            {
                value.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}

internal sealed class MigratorArguments
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public static MigratorArguments Parse(IEnumerable<string> args)
    {
        var parsed = new MigratorArguments();
        string? pending = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                pending = arg[2..];
                parsed._values[pending] = null;
            }
            else if (pending is not null)
            {
                parsed._values[pending] = arg;
                pending = null;
            }
            else
            {
                throw new ArgumentException($"Argumento inesperado: {arg}");
            }
        }

        return parsed;
    }

    public bool Flag(string name) => _values.ContainsKey(name);

    public string? Value(string name) => _values.GetValueOrDefault(name);

    public string Required(string name) =>
        Value(name) is { Length: > 0 } value ? value : throw new ArgumentException($"Falta --{name}.");
}
