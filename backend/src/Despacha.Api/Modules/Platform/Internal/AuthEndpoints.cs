using System.Security.Claims;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class OriginNotAllowedException()
    : DespachaException(StatusCodes.Status403Forbidden, "Origen no permitido.");

/// <summary>
/// Login y renovación (D-08, SEG-04, SEG-05). Expo recibe el refresh token en el cuerpo y lo guarda en
/// expo-secure-store; el admin web (client = "web") lo recibe en una cookie HttpOnly, Secure y SameSite=Strict
/// limitada a /api/auth, y las llamadas que usan esa cookie exigen un Origin permitido.
/// </summary>
internal static class AuthEndpoints
{
    public const string RefreshCookie = "__Secure-despacha-rt";
    private const string CookiePath = "/api/auth";
    private const string WebClient = "web";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest request, AuthService service, HttpContext http, CancellationToken ct) =>
            Respond(http, await service.LoginAsync(request.Login, request.Password, ct), request.Client == WebClient))
            .AllowAnonymous().RequireRateLimiting(RateLimitSettings.AuthenticationPolicy);

        auth.MapPost("/refresh", async ([FromBody] RefreshRequest? request, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var (token, web) = ReadRefreshToken(http, request?.RefreshToken);
            return Respond(http, await service.RefreshAsync(token ?? throw new InvalidRefreshTokenException(), ct), web);
        }).AllowAnonymous().RequireRateLimiting(RateLimitSettings.AuthenticationPolicy);

        auth.MapPost("/tenant", async (SelectTenantRequest request, ClaimsPrincipal user, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var userId = user.GetUserId() ?? throw new InvalidCredentialsException();
            var (token, web) = ReadRefreshToken(http, request.RefreshToken);
            return Respond(http, await service.SelectTenantAsync(userId, request.TenantId, token, ct), web);
        });

        auth.MapPost("/logout", async ([FromBody] RefreshRequest? request, AuthService service, HttpContext http, CancellationToken ct) =>
        {
            var (token, web) = ReadRefreshToken(http, request?.RefreshToken);
            if (token is not null)
            {
                await service.LogoutAsync(token, ct);
            }

            if (web)
            {
                http.Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
            }

            return TypedResults.NoContent();
        }).AllowAnonymous();

        auth.MapGet("/me", async (ClaimsPrincipal user, ITenantContext tenant, AuthService service, TenantAccessService access,
            UserManager<StaffUser> users, CancellationToken ct) =>
        {
            var userId = user.GetUserId() ?? throw new InvalidCredentialsException();
            var staff = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (staff is not { IsActive: true })
            {
                throw new InvalidCredentialsException();
            }

            var granted = tenant.TenantId is long tid ? await access.GetAsync(userId, tid, ct) : null;
            return TypedResults.Ok(new MeResponse(
                userId,
                staff.UserName!,
                staff.DisplayName,
                staff.RequiresPasswordChange,
                granted?.TenantId,
                granted?.Role,
                granted?.Permissions.Order(StringComparer.Ordinal).ToArray() ?? [],
                await service.MembershipsAsync(userId, ct)));
        });

        auth.MapPost("/password/forgot", async (ForgotPasswordRequest request, AuthService service, CancellationToken ct) =>
        {
            await service.RequestPasswordResetAsync(request.Login, ct);
            return TypedResults.Accepted((string?)null);
        }).AllowAnonymous().RequireRateLimiting(RateLimitSettings.AuthenticationPolicy);

        auth.MapPost("/password/reset", async Task<Results<NoContent, ValidationProblem>> (ResetPasswordRequest request, AuthService service, CancellationToken ct) =>
            ToResult(await service.ResetPasswordAsync(request.Login, request.Token, request.NewPassword, ct)))
            .AllowAnonymous().RequireRateLimiting(RateLimitSettings.AuthenticationPolicy);

        auth.MapPost("/password/change", async Task<Results<NoContent, ValidationProblem>> (ChangePasswordRequest request, ClaimsPrincipal user, AuthService service, CancellationToken ct) =>
            ToResult(await service.ChangePasswordAsync(user.GetUserId() ?? throw new InvalidCredentialsException(), request.CurrentPassword, request.NewPassword, ct)));
    }

    internal static Results<NoContent, ValidationProblem> ToResult(IdentityResult result) =>
        result.Succeeded
            ? TypedResults.NoContent()
            : TypedResults.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    private static Ok<AuthResponse> Respond(HttpContext http, AuthResult result, bool web)
    {
        if (web)
        {
            http.Response.Cookies.Append(RefreshCookie, result.Tokens.RefreshToken, CookieOptions(result.Tokens.RefreshExpiresAtUtc));
        }

        var expiresIn = (int)Math.Max(0, (result.Tokens.AccessExpiresAtUtc - DateTime.UtcNow).TotalSeconds);
        return TypedResults.Ok(new AuthResponse(
            result.Tokens.AccessToken,
            expiresIn,
            web ? null : result.Tokens.RefreshToken,
            result.TenantId,
            result.Memberships,
            result.RequiresPasswordChange));
    }

    private static (string? Token, bool Web) ReadRefreshToken(HttpContext http, string? fromBody)
    {
        if (!string.IsNullOrEmpty(fromBody))
        {
            return (fromBody, false);
        }

        if (!http.Request.Cookies.TryGetValue(RefreshCookie, out var fromCookie) || string.IsNullOrEmpty(fromCookie))
        {
            return (null, false);
        }

        var origin = http.Request.Headers.Origin.ToString();
        var allowed = http.RequestServices.GetRequiredService<IOptions<CorsSettings>>().Value.AllowedOrigins;
        if (string.IsNullOrEmpty(origin) || !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            throw new OriginNotAllowedException();
        }

        return (fromCookie, true);
    }

    private static CookieOptions CookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}

internal sealed record LoginRequest(string Login, string Password, string? Client);

internal sealed record RefreshRequest(string? RefreshToken);

internal sealed record SelectTenantRequest(long TenantId, string? RefreshToken);

internal sealed record ForgotPasswordRequest(string Login);

internal sealed record ResetPasswordRequest(string Login, string Token, string NewPassword);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record AuthResponse(
    string AccessToken,
    int ExpiresIn,
    string? RefreshToken,
    long? TenantId,
    IReadOnlyList<MembershipSummary> Memberships,
    bool RequiresPasswordChange);

internal sealed record MeResponse(
    long UserId,
    string Login,
    string DisplayName,
    bool RequiresPasswordChange,
    long? TenantId,
    string? Role,
    string[] Permissions,
    IReadOnlyList<MembershipSummary> Memberships);
