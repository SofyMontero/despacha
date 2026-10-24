using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Despacha.Api.Shared.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;
}

internal sealed record IssuedTokens(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc);

/// <summary>JWT corto (D-08) y refresh token opaco de un solo uso; en base solo queda su hash.</summary>
internal sealed class TokenService(
    ControlDbContext db,
    JwtSigningKey signingKey,
    IOptions<JwtSettings> jwt,
    IOptions<AuthOptions> auth,
    TimeProvider clock)
{
    public async Task<IssuedTokens> IssueAsync(long userId, long? tenantId, Guid familyId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var accessExpires = now.AddMinutes(auth.Value.AccessTokenMinutes);
        var claims = new Dictionary<string, object>
        {
            [DespachaClaims.Subject] = userId.ToString(CultureInfo.InvariantCulture),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
        };
        if (tenantId is long tid)
        {
            claims[DespachaClaims.TenantId] = tid.ToString(CultureInfo.InvariantCulture);
        }

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Value.Issuer,
            Audience = jwt.Value.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = accessExpires,
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var refreshExpires = now.AddDays(auth.Value.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TenantId = tenantId,
            TokenHash = Hash(refreshToken),
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedTokens(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

internal sealed class PlatformAudit(ControlDbContext db, TimeProvider clock)
{
    public async Task RecordAsync(string action, long? userId, long? tenantId, string? detail = null, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new PlatformAuditEvent
        {
            OccurredAtUtc = clock.GetUtcNow().UtcDateTime,
            Action = action,
            UserId = userId,
            TenantId = tenantId,
            Detail = detail,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Canal de entrega del enlace de recuperación. Se conecta a correo o WhatsApp en una fase posterior.</summary>
internal interface IPasswordResetDelivery
{
    Task SendAsync(StaffUser user, string resetToken, CancellationToken cancellationToken);
}

internal sealed class DisabledPasswordResetDelivery(ILogger<DisabledPasswordResetDelivery> logger) : IPasswordResetDelivery
{
    public Task SendAsync(StaffUser user, string resetToken, CancellationToken cancellationToken)
    {
        logger.LogWarning("Entrega de recuperación de contraseña deshabilitada; usuario {UserId} sin notificar.", user.Id);
        return Task.CompletedTask;
    }
}
