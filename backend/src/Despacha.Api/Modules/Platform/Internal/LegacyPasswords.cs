namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>Hashes bcrypt que dejó el PHP en <c>users.password</c> (<c>password_hash</c> de PHP genera <c>$2y$</c>).</summary>
internal static class LegacyPasswords
{
    private static readonly string[] Prefixes = ["$2y$", "$2a$", "$2b$"];

    public static bool IsBcrypt(string? value) =>
        value is { Length: 60 } && Prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));

    public static bool Verify(string password, string? legacyHash)
    {
        if (!IsBcrypt(legacyHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, legacyHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
