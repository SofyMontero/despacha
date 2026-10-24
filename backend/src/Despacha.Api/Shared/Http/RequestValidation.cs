using Microsoft.AspNetCore.Http.HttpResults;

namespace Despacha.Api.Shared.Http;

/// <summary>Acumula errores por campo y produce el mismo <c>ValidationProblem</c> (400) que el resto de la API.</summary>
public sealed class RequestValidation
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public RequestValidation Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            _errors[field] = messages = [];
        }

        messages.Add(message);
        return this;
    }

    public RequestValidation Required(string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Add(field, "Es obligatorio.");
        }

        return MaxLength(field, value, maxLength);
    }

    public RequestValidation MaxLength(string field, string? value, int maxLength) =>
        value is not null && value.Trim().Length > maxLength ? Add(field, $"Máximo {maxLength} caracteres.") : this;

    public RequestValidation Range(string field, decimal value, decimal min, decimal max, string message) =>
        value < min || value > max ? Add(field, message) : this;

    public ValidationProblem ToProblem() =>
        TypedResults.ValidationProblem(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal));

    public static ValidationProblem Single(string field, string message) => new RequestValidation().Add(field, message).ToProblem();

    /// <summary>Recorta y convierte en null los textos opcionales vacíos.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
