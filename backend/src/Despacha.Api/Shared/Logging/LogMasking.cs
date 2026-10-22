namespace Despacha.Api.Shared.Logging;

public static class LogMasking
{
    /// <summary>Deja visibles solo los últimos 4 dígitos.</summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length <= 4
            ? new string('*', digits.Length)
            : new string('*', digits.Length - 4) + digits[^4..];
    }
}
