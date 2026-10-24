using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Despacha.Api.Modules.Platform.Internal;

internal static class ControlPlatformServices
{
    /// <summary>Base de control e Identity. Compartido por la API y el Migrator; no aplica migraciones.</summary>
    public static IServiceCollection AddControlPlatform(this IServiceCollection services)
    {
        services.AddOptions<ControlDatabaseOptions>()
            .BindConfiguration(ControlDatabaseOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ControlDatabaseOptions>, ControlDatabaseOptionsValidator>();
        services.AddDbContext<ControlDbContext>((sp, options) =>
            options.UseDespachaMySql(sp.GetRequiredService<IOptions<ControlDatabaseOptions>>().Value.Control));

        services.AddDataProtection();
        services.AddIdentityCore<StaffUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ControlDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));

        services.AddScoped<PlatformAudit>();
        services.AddScoped<TenantProvisioningService>();
        return services;
    }
}

internal sealed record TenantProvisioningRequest(
    string Code,
    string Name,
    string DatabaseServer,
    int DatabasePort,
    string DatabaseName,
    string DatabaseSecretName,
    string AdminLogin,
    string AdminDisplayName,
    string AdminPassword);

/// <summary>Alta de empresa en la base de control: registro, base, módulos activos, roles plantilla y primer administrador (D-03).</summary>
internal sealed class TenantProvisioningService(ControlDbContext db, UserManager<StaffUser> users, PlatformAudit audit, TimeProvider clock)
{
    public async Task<long> ProvisionAsync(TenantProvisioningRequest request, CancellationToken cancellationToken = default)
    {
        if (await db.Tenants.AnyAsync(t => t.Code == request.Code, cancellationToken))
        {
            throw new InvalidOperationException($"La empresa '{request.Code}' ya existe.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var tenant = new Tenant { Code = request.Code, Name = request.Name };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        db.TenantDatabases.Add(new TenantDatabase
        {
            TenantId = tenant.Id,
            Server = request.DatabaseServer,
            Port = request.DatabasePort,
            DatabaseName = request.DatabaseName,
            SecretName = request.DatabaseSecretName,
        });
        db.TenantModules.AddRange(Permissions.Modules.Select(module => new TenantModule { TenantId = tenant.Id, Module = module, Enabled = true }));

        var roles = RoleTemplates.Defaults.Select(template => new Role
        {
            TenantId = tenant.Id,
            Name = template.Key,
            IsTemplate = true,
            Permissions = template.Value.Select(code => new RolePermission { Permission = code }).ToList(),
        }).ToList();
        db.Roles.AddRange(roles);
        await db.SaveChangesAsync(cancellationToken);

        var admin = await users.FindByNameAsync(request.AdminLogin);
        if (admin is null)
        {
            admin = new StaffUser { UserName = request.AdminLogin, DisplayName = request.AdminDisplayName, CreatedAtUtc = now };
            var created = await users.CreateAsync(admin, request.AdminPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException("No se pudo crear el administrador: " + string.Join(", ", created.Errors.Select(e => e.Code)));
            }
        }

        db.Memberships.Add(new Membership
        {
            UserId = admin.Id,
            TenantId = tenant.Id,
            RoleId = roles.Single(r => r.Name == RoleTemplates.Administrator).Id,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("tenant.provisioned", admin.Id, tenant.Id, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tenant.Id;
    }
}

/// <summary>Usada por las herramientas de EF Core y el Migrator para generar migraciones y scripts sin conectarse.</summary>
internal sealed class ControlDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ControlDbContext>
{
    public const string PlaceholderConnection = "Server=localhost;Database=despacha_control_design;User ID=design;";

    public ControlDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ControlDbContext>();
        builder.UseDespachaMySql(PlaceholderConnection);
        return new ControlDbContext(builder.Options);
    }
}
