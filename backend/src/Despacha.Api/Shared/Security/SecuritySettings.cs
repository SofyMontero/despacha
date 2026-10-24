using System.Security.Cryptography;
using System.Text;
using Despacha.Api.Shared.Runtime;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Despacha.Api.Shared.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Auth:Jwt";
    public const int MinimumSigningKeyLength = 32;

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

public sealed class RateLimitSettings
{
    public const string SectionName = "Security:RateLimit";

    /// <summary>Política aplicada a login, renovación y recuperación de contraseña.</summary>
    public const string AuthenticationPolicy = "auth";

    public int AuthPermitsPerMinute { get; set; } = 10;
}

public sealed class JwtSigningKey(IOptions<JwtSettings> settings, DespachaRuntime runtime)
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
            ? new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(JwtSettings.MinimumSigningKeyLength))
            : throw new InvalidOperationException("Falta Auth:Jwt:SigningKey.");
    }
}
