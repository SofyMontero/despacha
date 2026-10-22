namespace Despacha.Api.Shared.Errors;

/// <summary>Error esperado cuyo mensaje es seguro de mostrar al cliente.</summary>
public abstract class DespachaException(int statusCode, string publicMessage) : Exception(publicMessage)
{
    public int StatusCode { get; } = statusCode;
}
