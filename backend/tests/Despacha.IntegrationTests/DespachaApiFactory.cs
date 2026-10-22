using System.Text;
using Despacha.Api.Modules.Platform.Internal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
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

    private readonly SqliteConnection _control = new("DataSource=:memory:");
    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Auth:Jwt:SigningKey"] = SigningKey,
    };
    private readonly List<Action<IServiceCollection>> _services = [];

    public DespachaApiFactory()
    {
        _control.Open();
    }

    public string HostEnvironment { get; init; } = "Development";

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

    public static string CreateToken(string? tenantId = "7", string signingKey = SigningKey, bool expired = false)
    {
        var now = DateTime.UtcNow;
        var expires = expired ? now.AddMinutes(-5) : now.AddMinutes(10);
        var claims = new Dictionary<string, object> { ["sub"] = "usuario-prueba" };
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostEnvironment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ControlDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ControlDbContext>));
            services.AddDbContext<ControlDbContext>(options => options.UseSqlite(_control));

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
        }
    }
}
