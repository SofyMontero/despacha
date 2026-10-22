using System.Security.Cryptography;
using System.Text;
using Despacha.Api.Modules.Platform;
using Despacha.Api.Shared.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Despacha.Api.Hosting;

public sealed class JwtSettings
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Solo desde user-secrets o variables de entorno. Mínimo 32 caracteres.</summary>
    public string? SigningKey { get; set; }
}

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

internal static class SecurityExtensions
{
    public const string CorsPolicyName = "despacha";
    private const int MinimumSigningKeyLength = 32;

    public static IServiceCollection AddDespachaSecurity(this IServiceCollection services)
    {
        services.AddOptions<JwtSettings>().BindConfiguration(JwtSettings.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddSingleton<JwtSigningKey>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>, JwtSigningKey>((options, settings, signingKey) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.Key,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = DespachaClaims.Subject,
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddOptions<CorsSettings>().BindConfiguration(CorsSettings.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsSettings>, CorsSettingsValidator>();
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<CorsSettings>>((options, settings) =>
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (settings.Value.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(settings.Value.AllowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithHeaders("Authorization", "Content-Type");
                }
            }));

        return services;
    }

    internal sealed class JwtSigningKey(IOptions<JwtSettings> settings, DespachaRuntime runtime)
    {
        public SymmetricSecurityKey Key { get; } = Create(settings.Value, runtime);

        private static SymmetricSecurityKey Create(JwtSettings settings, DespachaRuntime runtime)
        {
            if (!string.IsNullOrEmpty(settings.SigningKey))
            {
                return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
            }

            // Solo en Local: clave efímera; los tokens dejan de valer al reiniciar.
            return runtime.IsLocal
                ? new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumSigningKeyLength))
                : throw new InvalidOperationException("Falta Auth:Jwt:SigningKey.");
        }
    }

    private sealed class JwtSettingsValidator(DespachaRuntime runtime) : IValidateOptions<JwtSettings>
    {
        public ValidateOptionsResult Validate(string? name, JwtSettings options)
        {
            if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            {
                return ValidateOptionsResult.Fail("Auth:Jwt:Issuer y Auth:Jwt:Audience son obligatorios.");
            }

            if (string.IsNullOrEmpty(options.SigningKey))
            {
                return runtime.IsLocal
                    ? ValidateOptionsResult.Success
                    : ValidateOptionsResult.Fail("Falta Auth:Jwt:SigningKey en la configuración externa.");
            }

            return options.SigningKey.Length < MinimumSigningKeyLength
                ? ValidateOptionsResult.Fail($"Auth:Jwt:SigningKey debe tener al menos {MinimumSigningKeyLength} caracteres.")
                : ValidateOptionsResult.Success;
        }
    }

    private sealed class CorsSettingsValidator(DespachaRuntime runtime) : IValidateOptions<CorsSettings>
    {
        public ValidateOptionsResult Validate(string? name, CorsSettings options)
        {
            foreach (var origin in options.AllowedOrigins)
            {
                var valid = origin != "*"
                    && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && runtime.IsLocal))
                    && uri.AbsolutePath == "/"
                    && string.IsNullOrEmpty(uri.Query);

                if (!valid)
                {
                    return ValidateOptionsResult.Fail($"Origen CORS no permitido: '{origin}'. Use orígenes explícitos (https fuera de Local).");
                }
            }

            return ValidateOptionsResult.Success;
        }
    }
}
