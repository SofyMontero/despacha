namespace Despacha.Api.Shared.Validation;

public static class PhoneNumbers
{
    /// <summary>
    /// Misma regla que <c>api_normalize_phone</c> del PHP (quita lo que no es dígito y antepone 57 a 10 dígitos),
    /// pero además exige que el resultado sea 57 más 10 dígitos. Devuelve el número en E.164 (<c>+57XXXXXXXXXX</c>).
    /// </summary>
    public static bool TryNormalizeColombian(string? input, out string e164)
    {
        e164 = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 10 && !digits.StartsWith("57", StringComparison.Ordinal))
        {
            digits = "57" + digits;
        }

        if (digits.Length != 12 || !digits.StartsWith("57", StringComparison.Ordinal))
        {
            return false;
        }

        e164 = "+" + digits;
        return true;
    }

    public static bool SameDigits(string? left, string? right)
    {
        var a = ComparableDigits(left);
        var b = ComparableDigits(right);
        return a.Length > 0 && a == b;
    }

    /// <summary>Diez dígitos nacionales equivalen al mismo número con el indicativo 57.</summary>
    private static string ComparableDigits(string? input)
    {
        var digits = DigitsOf(input);
        return digits.Length == 10 ? "57" + digits : digits;
    }

    /// <summary>Formas en que un teléfono histórico puede estar guardado para el mismo número E.164.</summary>
    public static string[] EquivalentStored(string e164)
    {
        var digits = DigitsOf(e164);
        var national = digits.Length == 12 && digits.StartsWith("57", StringComparison.Ordinal) ? digits[2..] : digits;
        return [e164, digits, national];
    }

    /// <summary>Solo los dígitos del texto, para buscar por teléfono con cualquier formato.</summary>
    public static string DigitsOf(string? input) =>
        input is null ? string.Empty : new string(input.Where(char.IsAsciiDigit).ToArray());
}
