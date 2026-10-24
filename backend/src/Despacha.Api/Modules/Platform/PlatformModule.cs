using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Modules;
using Microsoft.AspNetCore.Authorization;

namespace Despacha.Api.Modules.Platform;

internal sealed class PlatformModule() : ModuleBase("Platform")
{
    public override void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddControlPlatform();

        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName)
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 60, "Auth:AccessTokenMinutes debe estar entre 1 y 60.")
            .Validate(o => o.RefreshTokenDays is >= 1 and <= 90, "Auth:RefreshTokenDays debe estar entre 1 y 90.")
            .ValidateOnStart();
        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();
        services.AddSingleton<IPasswordResetDelivery, DisabledPasswordResetDelivery>();

        services.AddMemoryCache();
        services.AddScoped<TenantAccessService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantConnectionProvider>();
        services.AddScoped(typeof(ITenantDbContextFactory<>), typeof(TenantDbContextFactory<>));

        services.AddHealthChecks().AddDbContextCheck<ControlDbContext>("control-db", tags: ["ready"]);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        SessionEndpoints.Map(endpoints);
        AuthEndpoints.Map(endpoints);
        AdministrationEndpoints.Map(endpoints);
    }
}
