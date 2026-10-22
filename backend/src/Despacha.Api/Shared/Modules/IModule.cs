namespace Despacha.Api.Shared.Modules;

/// <summary>
/// Módulo del monolito (D-01). La interfaz pública de cada módulo vive en <c>Modules.&lt;Nombre&gt;</c>
/// y la implementación en <c>Modules.&lt;Nombre&gt;.Internal</c>, que ningún otro módulo puede usar.
/// </summary>
public interface IModule
{
    string Name { get; }

    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

public abstract class ModuleBase(string name) : IModule
{
    public string Name { get; } = name;

    public virtual void Register(IServiceCollection services, IConfiguration configuration)
    {
    }

    public virtual void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

public static class ModuleRegistry
{
    public static IServiceCollection AddDespachaModules(this IServiceCollection services, IConfiguration configuration)
    {
        var modules = typeof(ModuleRegistry).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IModule)Activator.CreateInstance(type)!);

        foreach (var module in modules)
        {
            module.Register(services, configuration);
            services.AddSingleton(module);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapDespachaModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<IModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
