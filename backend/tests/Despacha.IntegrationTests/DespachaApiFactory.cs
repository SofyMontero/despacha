using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Despacha.IntegrationTests;

/// <summary>Host real de la API con la base de control sustituida por SQLite en memoria. No usa servicios externos.</summary>
public sealed class DespachaApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "clave-de-firma-solo-para-pruebas-0123456789";
    public const string Issuer = "despacha-api";
    public const string Audience = "despacha-clients";
    public const string Password = "clave-de-prueba-segura";

    private readonly SqliteConnection _control = new("DataSource=:memory:");
    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Auth:Jwt:SigningKey"] = SigningKey,
        ["Security:RateLimit:AuthPermitsPerMinute"] = "1000",
    };
    private readonly List<Action<IServiceCollection>> _services = [];
    private bool _schemaCreated;
    private SqliteTenantDatabases? _tenantDatabases;

    public DespachaApiFactory()
    {
        _control.Open();
    }

    public string HostEnvironment { get; init; } = "Development";

    /// <summary>Si se indica, la API usa esta base de control real (desechable) en lugar de SQLite.</summary>
    public string? ControlConnectionString { get; init; }

    internal CapturingPasswordResetDelivery ResetDelivery { get; } = new();

    public DespachaApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    public DespachaApiFactory WithServices(Action<IServiceCollection> configure)
    {
        _services.Add(configure);
        return this;
    }

    /// <summary>Bases de empresa en SQLite y almacenamiento de imágenes en una carpeta temporal.</summary>
    public DespachaApiFactory WithSqliteTenantDatabases()
    {
        var databases = _tenantDatabases ??= new SqliteTenantDatabases();
        _settings["Storage:Provider"] = "Local";
        _settings["Storage:LocalRootPath"] = databases.MediaRoot;
        return WithServices(services =>
        {
            services.AddSingleton(databases);
            services.RemoveAll(typeof(ITenantDbContextFactory<>));
            services.AddScoped(typeof(ITenantDbContextFactory<>), typeof(SqliteTenantDbContextFactory<>));
        });
    }

    public static string CreateToken(string? tenantId = "7", string signingKey = SigningKey, bool expired = false, string subject = "usuario-prueba")
    {
        var now = DateTime.UtcNow;
        var expires = expired ? now.AddMinutes(-5) : now.AddMinutes(10);
        var claims = new Dictionary<string, object> { ["sub"] = subject };
        if (tenantId is not null)
        {
            claims["tid"] = tenantId;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = claims,
            IssuedAt = expires.AddMinutes(-15),
            NotBefore = expires.AddMinutes(-15),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>Crea una empresa con roles plantilla, módulos activos y su administrador, igual que el Migrator.</summary>
    public async Task<long> ProvisionTenantAsync(string code, string adminLogin)
    {
        using var scope = Services.CreateScope();
        EnsureSchema(scope.ServiceProvider);
        return await scope.ServiceProvider.GetRequiredService<TenantProvisioningService>().ProvisionAsync(new TenantProvisioningRequest(
            code, code, "localhost", 3306, $"despacha_{code}_local", $"tenant-{code}", adminLogin, adminLogin, Password));
    }

    /// <summary>Agrega a un usuario (nuevo o existente) a la empresa con un rol plantilla.</summary>
    public async Task<long> AddMemberAsync(long tenantId, string login, string role)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<StaffUser>>();
        var user = await users.FindByNameAsync(login);
        if (user is null)
        {
            user = new StaffUser { UserName = login, DisplayName = login };
            var created = await users.CreateAsync(user, Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Code)));
        }

        var roleId = await db.Roles.Where(r => r.TenantId == tenantId && r.Name == role).Select(r => r.Id).SingleAsync();
        db.Memberships.Add(new Membership { UserId = user.Id, TenantId = tenantId, RoleId = roleId });
        await db.SaveChangesAsync();
        return user.Id;
    }

    internal async Task WithControlDbAsync(Func<ControlDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ControlDbContext>());
    }

    internal void InvalidateAccess(long tenantId)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantAccessService>().InvalidateTenant(tenantId);
    }

    public static async Task<AuthDto> LoginAsync(HttpClient client, string login, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthDto>())!;
    }

    public static async Task<AuthDto> SelectTenantAsync(HttpClient client, AuthDto session, long tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/tenant")
        {
            Content = JsonContent.Create(new { tenantId, refreshToken = session.RefreshToken }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthDto>())!;
    }

    public static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostEnvironment);
        if (ControlConnectionString is not null)
        {
            _settings["ConnectionStrings:Control"] = ControlConnectionString;
            _schemaCreated = true;
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Los user-secrets del desarrollador pueden apuntar a TEST; las pruebas no los leen.
            var userSecrets = configuration.Sources
                .OfType<JsonConfigurationSource>()
                .Where(source => source.Path == "secrets.json")
                .ToArray();
            foreach (var source in userSecrets)
            {
                configuration.Sources.Remove(source);
            }

            configuration.AddInMemoryCollection(_settings);
        });
        builder.ConfigureTestServices(services =>
        {
            if (ControlConnectionString is null)
            {
                services.RemoveAll<DbContextOptions<ControlDbContext>>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ControlDbContext>));
                services.AddDbContext<ControlDbContext>(options => options.UseSqlite(_control));
            }

            services.RemoveAll<IPasswordResetDelivery>();
            services.AddSingleton<IPasswordResetDelivery>(ResetDelivery);

            foreach (var configure in _services)
            {
                configure(services);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _control.Dispose();
            _tenantDatabases?.Dispose();
        }
    }

    private void EnsureSchema(IServiceProvider services)
    {
        if (!_schemaCreated)
        {
            services.GetRequiredService<ControlDbContext>().Database.EnsureCreated();
            _schemaCreated = true;
        }
    }
}

public sealed record MembershipDto(long TenantId, string TenantCode, string TenantName, string Role);

public sealed record AuthDto(string AccessToken, int ExpiresIn, string? RefreshToken, long? TenantId, MembershipDto[] Memberships, bool RequiresPasswordChange);

internal sealed class CapturingPasswordResetDelivery : IPasswordResetDelivery
{
    public ConcurrentDictionary<long, string> Tokens { get; } = new();

    public Task SendAsync(StaffUser user, string resetToken, CancellationToken cancellationToken)
    {
        Tokens[user.Id] = resetToken;
        return Task.CompletedTask;
    }
}
