using Despacha.Api.Integrations.Storage;
using Despacha.Api.Integrations.WhatsApp;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Modules;
using Despacha.Api.Shared.Runtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/auth/password/forgot",
        "/api/auth/password/reset",
        "/media/{**key}",
    };

    /// <summary>Host ASP.NET del perfil "test": usa appsettings.Test.json y su propio archivo de user-secrets.</summary>
    public const string TestHostEnvironment = "Test";

    public const string TestUserSecretsId = "despacha-api-test";

    public static WebApplicationBuilder AddDespacha(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsEnvironment(TestHostEnvironment))
        {
            builder.Configuration.AddUserSecrets(TestUserSecretsId);
        }

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

        services.AddDespachaRuntime();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddDespachaSecurity();
        services.AddOpenApi();

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
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseTenantResolution();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous();

        app.MapDespachaMedia();
        app.MapDespachaModules();
        return app;
    }
}
