using Despacha.Api.Integrations.WhatsApp;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Persistence;
using Despacha.Api.Shared.Runtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

namespace Despacha.Api.Hosting;

public static class DespachaHostingExtensions
{
    /// <summary>Rutas públicas. Todo lo demás exige autenticación por la política por defecto.</summary>
    public static readonly IReadOnlySet<string> PublicRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/health/live",
        "/health/ready",
        "/openapi/{documentName}.json",
    };

    public static WebApplicationBuilder AddDespacha(this WebApplicationBuilder builder)
    {
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        var services = builder.Services;

        services.AddSerilog((sp, logger) =>
        {
            logger.ReadFrom.Configuration(sp.GetRequiredService<IConfiguration>()).Enrich.FromLogContext();
            if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                logger.WriteTo.Console();
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });

        services.AddOptions<DespachaOptions>().BindConfiguration(DespachaOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DespachaOptions>, DespachaOptionsValidator>();
        services.AddSingleton(sp => new DespachaRuntime(DespachaEnvironmentResolver.Resolve(
            sp.GetRequiredService<IOptions<DespachaOptions>>().Value.Environment,
            sp.GetRequiredService<IHostEnvironment>())));

        services.AddOptions<DatabaseSafetyOptions>().BindConfiguration(DatabaseSafetyOptions.SectionName);
        services.AddSingleton<DatabaseSafetyGuard>();

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddDespachaSecurity();
        services.AddOpenApi();
        services.AddSingleton(TimeProvider.System);

        services.AddDespachaIntegrations();
        services.AddDespachaModules(builder.Configuration);

        return builder;
    }

    public static WebApplication UseDespacha(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging();

        if (app.Services.GetRequiredService<DespachaRuntime>().IsLocal)
        {
            app.MapOpenApi().AllowAnonymous();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Despacha API"));
        }

        app.UseCors(SecurityExtensions.CorsPolicyName);
        app.UseAuthentication();
        app.UseTenantResolution();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous();

        app.MapDespachaModules();
        return app;
    }
}
