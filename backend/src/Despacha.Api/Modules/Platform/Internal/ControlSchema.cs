using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using static Despacha.Api.Shared.Persistence.ModernizedSchemaGuard;

namespace Despacha.Api.Modules.Platform.Internal;

/// <summary>
/// Comprueba, sin escribir, que la base de control tiene todas las columnas que mapea
/// <see cref="ControlDbContext"/> (script 01, versión <see cref="ControlDbContext.ControlSchemaVersion"/>).
/// La lista sale del modelo, así que una columna nueva sin script se detecta sola.
/// </summary>
internal static class ControlSchema
{
    private static IReadOnlyCollection<ColumnRequirement>? _requirements;

    /// <summary>
    /// Columnas históricas que la API no escribe: sin valor por defecto, un INSERT fallaría.
    /// Una base nueva (Local) no las tiene.
    /// </summary>
    private static readonly ColumnRequirement[] UnmappedWithDefault =
    [
        Column("users", "direccion", requiresDefault: true, onlyIfPresent: true),
        Column("users", "rol", requiresDefault: true, onlyIfPresent: true),
        Column("roles", "rol_estado", requiresDefault: true, onlyIfPresent: true),
    ];

    public static IReadOnlyCollection<ColumnRequirement> Requirements(IModel model) =>
        _requirements ??=
        [
            .. model.GetRelationalModel().Tables.SelectMany(table => table.Columns.Select(column => Column(table.Name, column.Name))),
            .. UnmappedWithDefault,
        ];

    public static async Task EnsureReadyAsync(ControlDbContext db, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        try
        {
            await EnsureAsync(db.Database.GetConnectionString()!, ControlDbContext.ControlSchemaVersion, Requirements(db.Model), cancellationToken);
        }
        catch (DatabaseConnectionBlockedException ex)
        {
            throw new ControlSchemaNotReadyException(ex.Message);
        }
    }
}

internal sealed class ControlSchemaNotReadyException(string message)
    : DespachaException(StatusCodes.Status503ServiceUnavailable, message);

/// <summary>Las rutas /api no abren la base de control hasta confirmar su esquema.</summary>
internal sealed class ControlSchemaMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ControlDbContext db)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            await ControlSchema.EnsureReadyAsync(db, context.RequestAborted);
        }

        await next(context);
    }
}
