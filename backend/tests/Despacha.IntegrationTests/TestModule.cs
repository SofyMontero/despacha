using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Despacha.IntegrationTests;

/// <summary>Módulo que solo existe en las pruebas para provocar errores controlados.</summary>
internal sealed class TestModule : IModule
{
    public const string InternalDetail = "detalle-interno-que-no-debe-salir";

    public string Name => "Test";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/test/boom", string () => throw new InvalidOperationException(InternalDetail)).AllowAnonymous();

        endpoints.MapGet("/test/tenant-db", async (ITenantDbContextFactory<ProbeDbContext> factory) =>
        {
            await using var context = await factory.CreateAsync();
            return Results.Ok();
        }).AllowAnonymous();
    }
}

internal sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);
