using System.Text;
using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Despacha.Api.Shared.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Despacha.Migrator;

/// <summary>
/// Esquema de la base de control y de las bases de empresa (D-07). La API nunca migra al arrancar.
/// La base de control de TEST y PROD es la histórica: se amplía con los scripts de database/proposed,
/// no con este programa.
/// Comandos:
///   script [--output archivo.sql]   SQL de la base de control para una base nueva y vacía; no se conecta.
///   apply --confirm                 Crea la base de control en ConnectionStrings:Control solo si está vacía.
///                                   Se niega si la base tiene tablas del PHP heredado.
///   set-password --login L --confirm
///                                   Define la contraseña de un usuario del personal (captura oculta o
///                                   variable DESPACHA_NEW_PASSWORD). Revoca sus sesiones.
/// Con DOTNET_ENVIRONMENT=Test lee los user-secrets de la API (despacha-api-test).
///   provision-tenant --code C --name N --db-server S --db-name D --db-secret K --admin-login L [--admin-name A] [--db-port P]
///                                   Contraseña del administrador: variable DESPACHA_ADMIN_PASSWORD o captura oculta.
///   tenant-script [--module M] [--from Migracion] [--output archivo.sql]
///                                   SQL de los módulos de la base de empresa para revisión; no se conecta.
///   tenant-apply --tenant C --confirm [--module M]
///                                   Crea, en una base vacía, las tablas del modelo actual (categorias, productos,
///                                   promociones, bares, clientes). No aplica las migraciones viejas de catalog_*.
///                                   Si la base ya tiene el esquema moderno, no cambia nada.
///                                   Se niega si hay tablas del PHP sin las columnas de 5D.20261024.02.
/// </summary>
internal static class MigratorProgram
{
    public const string AdminPasswordVariable = "DESPACHA_ADMIN_PASSWORD";
    public const string NewPasswordVariable = "DESPACHA_NEW_PASSWORD";
    public const string TestEnvironment = "Test";
    public const string TestUserSecretsId = "despacha-api-test";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: script | apply --confirm | set-password --login L --confirm | provision-tenant ... | tenant-script | tenant-apply --tenant C --confirm (ver README).");
            return 1;
        }

        var options = MigratorArguments.Parse(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "script" => Script(options),
                "apply" => await ApplyAsync(options),
                "set-password" => await SetPasswordAsync(options),
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
        var legacy = await LegacySchemaGuard.FindAsync(connectionString);
        if (legacy.Count > 0)
        {
            try
            {
                await ModernizedSchemaGuard.EnsureReadyAsync(connectionString);
            }
            catch (DatabaseConnectionBlockedException)
            {
                throw new DatabaseConnectionBlockedException(
                    $"La base tiene tablas del sistema heredado ({string.Join(", ", legacy)}) sin el esquema {ModernizedSchemaGuard.MigrationVersion}. No se migra.");
            }

        }

        Console.WriteLine($"Ambiente {services.GetRequiredService<DespachaRuntime>().Environment}; empresa '{code}'.");
        foreach (var schema in TenantSchemas(services, options.Value("module")))
        {
            await using var context = schema.CreateContext(services, connectionString);
            await CreateModelTablesAsync(context, schema.Module);
        }

        Console.WriteLine("Tablas de la empresa creadas.");
        return 0;
    }

    /// <summary>
    /// Crea las tablas del modelo actual. Las migraciones EF de catalog_* y customers_* quedan sin aplicar:
    /// no coinciden con las tablas reales y no deben correr sobre una base heredada.
    /// </summary>
    private static async Task CreateModelTablesAsync(DbContext context, string module)
    {
        var expected = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()";
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    present.Add(reader.GetString(0));
                }
            }

            var found = expected.Count(present.Contains);
            if (found == expected.Length)
            {
                Console.WriteLine($"Módulo {module}: las tablas ya existen. No se cambió nada.");
                return;
            }

            if (found > 0)
            {
                throw new InvalidOperationException($"La base tiene solo parte de las tablas de {module}. No se cambió nada.");
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        context.GetService<IRelationalDatabaseCreator>().CreateTables();
        Console.WriteLine($"Módulo {module}: tablas creadas ({string.Join(", ", expected)}).");
    }

    /// <summary>Esquema de control completo desde el modelo, para una base vacía. No hay migraciones de control.</summary>
    internal static string GenerateScript()
    {
        using var context = new ControlDbContextDesignTimeFactory().CreateDbContext([]);
        return $"-- Esquema de control {ControlDbContext.ControlSchemaVersion} para una base VACÍA (Local).{Environment.NewLine}"
            + $"-- En Test y Prod usar database/proposed/01_identidad_sobre_tablas_existentes_NO_EJECUTAR.sql.{Environment.NewLine}"
            + context.Database.GenerateCreateScript();
    }

    private static int Script(MigratorArguments options)
    {
        var script = GenerateScript();
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
        await LegacySchemaGuard.EnsureNotLegacyAsync(db.Database.GetConnectionString()!);

        Console.WriteLine($"Ambiente {scope.ServiceProvider.GetRequiredService<DespachaRuntime>().Environment}.");
        var created = await db.Database.EnsureCreatedAsync();
        Console.WriteLine(created ? "Esquema de control creado." : "La base ya tenía tablas: no se cambió nada.");
        return 0;
    }

    /// <summary>
    /// Para usuarios sin hash de Identity cuya clave histórica no es bcrypt: no se lee ni se compara esa clave,
    /// se define una nueva que cumpla la política. Revoca sus sesiones.
    /// </summary>
    private static async Task<int> SetPasswordAsync(MigratorArguments options)
    {
        var login = options.Required("login");
        if (!options.Flag("confirm"))
        {
            return Fail("set-password exige --confirm.");
        }

        using var host = BuildHost();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await ControlSchema.EnsureReadyAsync(services.GetRequiredService<ControlDbContext>(), CancellationToken.None);
        var users = services.GetRequiredService<UserManager<StaffUser>>();
        var user = await users.FindByNameAsync(login)
            ?? throw new ArgumentException($"El usuario '{login}' no existe.");

        Console.WriteLine($"Ambiente {services.GetRequiredService<DespachaRuntime>().Environment}; usuario id {user.Id}.");
        var password = Environment.GetEnvironmentVariable(NewPasswordVariable) ?? ReadHidden("Contraseña nueva: ");
        if (Environment.GetEnvironmentVariable(NewPasswordVariable) is null && ReadHidden("Repita la contraseña: ") != password)
        {
            return Fail("Las contraseñas no coinciden.");
        }

        var result = user.PasswordHash is null
            ? await users.AddPasswordAsync(user, password)
            : await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), password);
        if (!result.Succeeded)
        {
            return Fail("No se cambió la contraseña: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        user.RequiresPasswordChange = false;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        var db = services.GetRequiredService<ControlDbContext>();
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.RevokedReason, "password"));
        db.AuditEvents.Add(new PlatformAuditEvent { OccurredAtUtc = now, Action = "password.set_by_operator", UserId = user.Id });
        await db.SaveChangesAsync();

        Console.WriteLine("Contraseña definida.");
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
        if (builder.Environment.IsEnvironment(TestEnvironment))
        {
            builder.Configuration
                .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"), optional: false)
                .AddUserSecrets(TestUserSecretsId);
        }

        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddDespachaRuntime();
        builder.Services.AddOptions<JwtSettings>().BindConfiguration(JwtSettings.SectionName);
        builder.Services.AddSingleton<JwtSigningKey>();
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
