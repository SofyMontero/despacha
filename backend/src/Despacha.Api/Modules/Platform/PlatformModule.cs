using Despacha.Api.Modules.Platform.Internal;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;
using Microsoft.Extensions.Options;

namespace Despacha.Api.Modules.Platform;

internal sealed class PlatformModule() : ModuleBase("Platform")
{
    public override void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ControlDatabaseOptions>()
            .BindConfiguration(ControlDatabaseOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ControlDatabaseOptions>, ControlDatabaseOptionsValidator>();
        services.AddDbContext<ControlDbContext>((sp, options) =>
            options.UseDespachaMySql(sp.GetRequiredService<IOptions<ControlDatabaseOptions>>().Value.Control));

        services.AddMemoryCache();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantConnectionProvider>();
        services.AddScoped(typeof(ITenantDbContextFactory<>), typeof(TenantDbContextFactory<>));

        services.AddHealthChecks().AddDbContextCheck<ControlDbContext>("control-db", tags: ["ready"]);
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints) => SessionEndpoints.Map(endpoints);
}
