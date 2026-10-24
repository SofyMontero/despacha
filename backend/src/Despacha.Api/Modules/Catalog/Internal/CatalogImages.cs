using Despacha.Api.Integrations.Storage;
using Despacha.Api.Shared.Http;
using Despacha.Api.Shared.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Despacha.Api.Modules.Catalog.Internal;

/// <summary>Reemplazo y retiro de la imagen de una fila del catálogo. El archivo anterior se borra solo si es propio.</summary>
internal static class CatalogImages
{
    public static string Folder(long tenantId, string kind) => $"t{tenantId}/catalog/{kind}";

    public static async Task<ValidationProblem?> ReplaceAsync<T>(
        CatalogDbContext db, T entity, IFormFile? file, string folder, IImageStorage storage, long? userId, DateTime nowUtc, CancellationToken ct)
        where T : TenantEntity, IHasImage
    {
        if (!storage.CanSave)
        {
            throw new StorageUnavailableException();
        }

        var (image, error) = await ImageValidation.ValidateAsync(file, ct);
        if (image is null)
        {
            return RequestValidation.Single("File", error!);
        }

        var previous = entity.ImageKey;
        var key = await storage.SaveAsync(folder, image, ct);
        entity.ImageKey = key;
        entity.MarkUpdated(userId, nowUtc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        if (previous is not null)
        {
            await storage.DeleteAsync(previous, CancellationToken.None);
        }

        return null;
    }

    public static async Task RemoveAsync<T>(CatalogDbContext db, T entity, IImageStorage storage, long? userId, DateTime nowUtc, CancellationToken ct)
        where T : TenantEntity, IHasImage
    {
        var previous = entity.ImageKey;
        if (previous is null)
        {
            return;
        }

        entity.ImageKey = null;
        entity.MarkUpdated(userId, nowUtc);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, CancellationToken.None);
    }
}
