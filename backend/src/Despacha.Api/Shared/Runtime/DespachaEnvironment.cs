using Microsoft.Extensions.Options;

namespace Despacha.Api.Shared.Runtime;

/// <summary>Ambiente lógico del despliegue (D-04). Lo fija la configuración del servidor, nunca el cliente.</summary>
public enum DespachaEnvironment
{
    Local,
    Test,
    Prod,
}

public sealed class DespachaOptions
{
    public const string SectionName = "Despacha";

    public string Environment { get; set; } = nameof(DespachaEnvironment.Local);
}

public sealed record DespachaRuntime(DespachaEnvironment Environment)
{
    public bool IsLocal => Environment == DespachaEnvironment.Local;
}

public static class DespachaEnvironmentResolver
{
    public static DespachaEnvironment Resolve(string? configured, IHostEnvironment host)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? nameof(DespachaEnvironment.Local) : configured.Trim();

        if (!Enum.TryParse<DespachaEnvironment>(value, ignoreCase: true, out var environment)
            || !Enum.GetNames<DespachaEnvironment>().Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Despacha:Environment '{value}' no es válido. Valores permitidos: Local, Test, Prod.");
        }

        if (environment == DespachaEnvironment.Prod && !host.IsProduction())
        {
            throw new InvalidOperationException("Despacha:Environment=Prod solo se permite con ASPNETCORE_ENVIRONMENT=Production.");
        }

        if (environment == DespachaEnvironment.Local && host.IsProduction())
        {
            throw new InvalidOperationException("Un host Production debe declarar Despacha:Environment=Test o Prod.");
        }

        return environment;
    }
}

internal sealed class DespachaOptionsValidator(IHostEnvironment host) : IValidateOptions<DespachaOptions>
{
    public ValidateOptionsResult Validate(string? name, DespachaOptions options)
    {
        try
        {
            DespachaEnvironmentResolver.Resolve(options.Environment, host);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
