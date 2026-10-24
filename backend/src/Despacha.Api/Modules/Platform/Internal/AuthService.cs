using Despacha.Api.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class InvalidCredentialsException()
    : DespachaException(StatusCodes.Status401Unauthorized, "Usuario o contraseña inválidos, o cuenta bloqueada temporalmente.");

internal sealed class InvalidRefreshTokenException()
    : DespachaException(StatusCodes.Status401Unauthorized, "La sesión expiró. Inicie sesión de nuevo.");

internal sealed class TenantAccessDeniedException()
    : DespachaException(StatusCodes.Status403Forbidden, "No tiene acceso a esa empresa.");

internal sealed record MembershipSummary(long TenantId, string TenantCode, string TenantName, string Role);

internal sealed record AuthResult(IssuedTokens Tokens, long? TenantId, IReadOnlyList<MembershipSummary> Memberships, bool RequiresPasswordChange);

internal sealed class AuthService(
    ControlDbContext db,
    UserManager<StaffUser> users,
    IPasswordHasher<StaffUser> hasher,
    TokenService tokens,
    PlatformAudit audit,
    IPasswordResetDelivery resetDelivery,
    TimeProvider clock)
{
    private const string Rotated = "rotated";
    private static string? s_dummyHash;

    public async Task<AuthResult> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(login) ? null : await users.FindByNameAsync(login.Trim());
        if (user is null || !user.IsActive)
        {
            // Igualar el costo para no revelar si el usuario existe.
            s_dummyHash ??= hasher.HashPassword(new StaffUser(), Guid.NewGuid().ToString());
            hasher.VerifyHashedPassword(new StaffUser(), s_dummyHash, password ?? string.Empty);
            await audit.RecordAsync("login.failed", user?.Id, null, cancellationToken: cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (await users.IsLockedOutAsync(user))
        {
            await audit.RecordAsync("login.locked", user.Id, null, cancellationToken: cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (!await users.CheckPasswordAsync(user, password ?? string.Empty))
        {
            await users.AccessFailedAsync(user);
            await audit.RecordAsync("login.failed", user.Id, null, cancellationToken: cancellationToken);
            throw new InvalidCredentialsException();
        }

        await users.ResetAccessFailedCountAsync(user);
        var memberships = await MembershipsAsync(user.Id, cancellationToken);
        long? tenantId = memberships.Count == 1 ? memberships[0].TenantId : null;
        var issued = await tokens.IssueAsync(user.Id, tenantId, Guid.NewGuid(), cancellationToken);
        await audit.RecordAsync("login.succeeded", user.Id, tenantId, cancellationToken: cancellationToken);
        return new AuthResult(issued, tenantId, memberships, user.RequiresPasswordChange);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenService.Hash(refreshToken);
        var current = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        var now = clock.GetUtcNow().UtcDateTime;
        if (current.RevokedAtUtc is not null)
        {
            if (current.RevokedReason == Rotated)
            {
                await RevokeFamilyAsync(current.FamilyId, "reuse", cancellationToken);
                await audit.RecordAsync("token.reuse_detected", current.UserId, current.TenantId, cancellationToken: cancellationToken);
            }

            throw new InvalidRefreshTokenException();
        }

        if (current.ExpiresAtUtc <= now)
        {
            throw new InvalidRefreshTokenException();
        }

        var claimed = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.RevokedReason, Rotated), cancellationToken);
        if (claimed == 0)
        {
            await RevokeFamilyAsync(current.FamilyId, "reuse", cancellationToken);
            await audit.RecordAsync("token.reuse_detected", current.UserId, current.TenantId, cancellationToken: cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        var user = await users.FindByIdAsync(current.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var memberships = user is { IsActive: true } ? await MembershipsAsync(user.Id, cancellationToken) : [];
        if (user is not { IsActive: true } || (current.TenantId is long tid && memberships.All(m => m.TenantId != tid)))
        {
            await RevokeFamilyAsync(current.FamilyId, "access_lost", cancellationToken);
            throw new InvalidRefreshTokenException();
        }

        var issued = await tokens.IssueAsync(user.Id, current.TenantId, current.FamilyId, cancellationToken);
        return new AuthResult(issued, current.TenantId, memberships, user.RequiresPasswordChange);
    }

    /// <summary>Cambiar de empresa emite tokens nuevos tras revalidar la membresía (D-05).</summary>
    public async Task<AuthResult> SelectTenantAsync(long userId, long tenantId, string? currentRefreshToken, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var memberships = user is { IsActive: true } ? await MembershipsAsync(userId, cancellationToken) : [];
        if (memberships.All(m => m.TenantId != tenantId))
        {
            await audit.RecordAsync("tenant.denied", userId, tenantId, cancellationToken: cancellationToken);
            throw new TenantAccessDeniedException();
        }

        if (!string.IsNullOrEmpty(currentRefreshToken))
        {
            await RevokeAsync(currentRefreshToken, "tenant_switch", cancellationToken);
        }

        var issued = await tokens.IssueAsync(userId, tenantId, Guid.NewGuid(), cancellationToken);
        await audit.RecordAsync("tenant.selected", userId, tenantId, cancellationToken: cancellationToken);
        return new AuthResult(issued, tenantId, memberships, user!.RequiresPasswordChange);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken) =>
        RevokeAsync(refreshToken, "logout", cancellationToken);

    /// <summary>Siempre responde igual para no revelar si el usuario existe.</summary>
    public async Task RequestPasswordResetAsync(string login, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(login) ? null : await users.FindByNameAsync(login.Trim());
        if (user is not { IsActive: true })
        {
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await resetDelivery.SendAsync(user, token, cancellationToken);
        await audit.RecordAsync("password.reset_requested", user.Id, null, cancellationToken: cancellationToken);
    }

    public async Task<IdentityResult> ResetPasswordAsync(string login, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(login) ? null : await users.FindByNameAsync(login.Trim());
        if (user is not { IsActive: true })
        {
            return IdentityResult.Failed(users.ErrorDescriber.InvalidToken());
        }

        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            await AfterPasswordChangedAsync(user, "password.reset", cancellationToken);
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }

        return result;
    }

    public async Task<IdentityResult> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (user is not { IsActive: true })
        {
            throw new InvalidCredentialsException();
        }

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            await AfterPasswordChangedAsync(user, "password.changed", cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<MembershipSummary>> MembershipsAsync(long userId, CancellationToken cancellationToken)
    {
        var rows = await db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Join(db.Tenants.Where(t => t.Status == TenantStatus.Active), m => m.TenantId, t => t.Id,
                (m, t) => new { t.Id, t.Code, t.Name, Role = m.Role.Name })
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(m => new MembershipSummary(m.Id, m.Code, m.Name, m.Role)).ToList();
    }

    private async Task AfterPasswordChangedAsync(StaffUser user, string action, CancellationToken cancellationToken)
    {
        user.RequiresPasswordChange = false;
        await users.UpdateAsync(user);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.RevokedReason, "password"), cancellationToken);
        await audit.RecordAsync(action, user.Id, null, cancellationToken: cancellationToken);
    }

    private async Task RevokeAsync(string refreshToken, string reason, CancellationToken cancellationToken)
    {
        var hash = TokenService.Hash(refreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(cancellationToken);
        if (familyId is Guid family)
        {
            await RevokeFamilyAsync(family, reason, cancellationToken);
        }
    }

    private Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.RevokedReason, reason), cancellationToken);
    }
}
