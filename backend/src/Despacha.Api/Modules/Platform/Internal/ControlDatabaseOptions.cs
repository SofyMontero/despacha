using Despacha.Api.Shared.Persistence;
using Microsoft.Extensions.Options;

namespace Despacha.Api.Modules.Platform.Internal;

internal sealed class ControlDatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Control { get; set; } = string.Empty;
}

internal sealed class ControlDatabaseOptionsValidator(DatabaseSafetyGuard guard) : IValidateOptions<ControlDatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, ControlDatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Control))
        {
            return ValidateOptionsResult.Fail("Falta ConnectionStrings:Control.");
        }

        try
        {
            guard.EnsureAllowed(options.Control);
            return ValidateOptionsResult.Success;
        }
        catch (DatabaseConnectionBlockedException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail("ConnectionStrings:Control no es una cadena de conexión MySQL válida.");
        }
    }
}
