using System.Text.RegularExpressions;
using Despacha.Api.Shared.Errors;
using Despacha.Api.Shared.Runtime;
using Microsoft.Extensions.Options;

namespace Despacha.Api.Integrations.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary><c>Local</c> o <c>Disabled</c>. Si no se indica: Local en el ambiente Local y Disabled en los demás (D-18 sigue pendiente).</summary>
    public string? Provider { get; set; }

    /// <summary>Carpeta del adaptador local. Debe estar fuera del repositorio.</summary>
    public string? LocalRootPath { get; set; }

    /// <summary>URL pública, de solo lectura, de <c>sistema/assets/img/</c> en Hostinger, para las imágenes heredadas.</summary>
    public string? LegacyBaseUrl { get; set; }
}

public sealed record ValidatedImage(byte[] Content, string Extension, string ContentType);

/// <summary>Guarda imágenes fuera de la base; la base solo guarda la clave relativa que devuelve <see cref="SaveAsync"/>.</summary>
public interface IImageStorage
{
    bool CanSave { get; }

    Task<string> SaveAsync(string folder, ValidatedImage image, CancellationToken cancellationToken);

    /// <summary>Borra el archivo propio. Las claves heredadas nunca se borran: el archivo vive en Hostinger.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    string? GetUrl(string? key);
}

public sealed class StorageUnavailableException()
    : DespachaException(StatusCodes.Status503ServiceUnavailable, "El almacenamiento de imágenes no está configurado en este ambiente.");

public static partial class ImageKeys
{
    /// <summary>Prefijo de las imágenes del PHP; el resto de la clave es la ruta relativa a <c>sistema/assets/img/</c>.</summary>
    public const string LegacyPrefix = "legacy/";

    public static bool IsLegacy(string key) =>
        key.StartsWith(LegacyPrefix, StringComparison.Ordinal) || IsBareFileName(key);

    /// <summary>Nombre de archivo del PHP, sin ruta. No se borra ni se pisa.</summary>
    public static bool IsBareFileName(string key) =>
        key.Length is > 0 and <= 300
        && !key.Contains('/')
        && !key.Contains('\\')
        && !key.Contains("..", StringComparison.Ordinal);

    public static bool IsValidFolder(string folder) => FolderPattern().IsMatch(folder);

    public static bool IsValidOwnKey(string key) => OwnKeyPattern().IsMatch(key);

    [GeneratedRegex("^[a-z0-9-]+(/[a-z0-9-]+)*$")]
    private static partial Regex FolderPattern();

    [GeneratedRegex("^[a-z0-9-]+(/[a-z0-9-]+)*/[a-f0-9]{32}\\.(jpg|png|gif|webp)$")]
    private static partial Regex OwnKeyPattern();
}

public static class ImageValidation
{
    public const long MaxBytes = 3 * 1024 * 1024;

    /// <summary>Mismos formatos y tamaño que el PHP (jpg, png, gif, webp; 3 MB), pero el tipo se verifica por contenido.</summary>
    public static async Task<(ValidatedImage? Image, string? Error)> ValidateAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, "Seleccione una imagen.");
        }

        if (file.Length > MaxBytes)
        {
            return (null, "La imagen no debe superar 3 MB.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        return Detect(content) is { } format
            ? (new ValidatedImage(content, format.Extension, format.ContentType), null)
            : (null, "Formato no permitido. Use jpg, png, gif o webp.");
    }

    internal static (string Extension, string ContentType)? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith((byte[])[0xFF, 0xD8, 0xFF]))
        {
            return ("jpg", "image/jpeg");
        }

        if (content.StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ("png", "image/png");
        }

        if (content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8))
        {
            return ("gif", "image/gif");
        }

        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            return ("webp", "image/webp");
        }

        return null;
    }

    public static string ContentTypeOf(string key) => Path.GetExtension(key) switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
}

internal abstract class ImageStorageBase(StorageOptions options) : IImageStorage
{
    public abstract bool CanSave { get; }

    public abstract Task<string> SaveAsync(string folder, ValidatedImage image, CancellationToken cancellationToken);

    public abstract Task DeleteAsync(string key, CancellationToken cancellationToken);

    public string? GetUrl(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (ImageKeys.IsBareFileName(key))
        {
            if (string.IsNullOrWhiteSpace(options.LegacyBaseUrl))
            {
                return null;
            }

            return options.LegacyBaseUrl.TrimEnd('/') + "/licores/" + Uri.EscapeDataString(key);
        }

        if (!ImageKeys.IsLegacy(key))
        {
            return GetOwnUrl(key);
        }

        if (string.IsNullOrWhiteSpace(options.LegacyBaseUrl))
        {
            return null;
        }

        var path = string.Join('/', key[ImageKeys.LegacyPrefix.Length..].Split('/').Select(Uri.EscapeDataString));
        return options.LegacyBaseUrl.TrimEnd('/') + "/" + path;
    }

    protected abstract string? GetOwnUrl(string key);
}

/// <summary>Adaptador para Local y pruebas. Sirve los archivos por <c>/media/{clave}</c>.</summary>
internal sealed class LocalImageStorage(StorageOptions options, string rootPath) : ImageStorageBase(options)
{
    public const string PublicPath = "/media/";

    public string RootPath { get; } = Path.GetFullPath(rootPath);

    public override bool CanSave => true;

    public override async Task<string> SaveAsync(string folder, ValidatedImage image, CancellationToken cancellationToken)
    {
        if (!ImageKeys.IsValidFolder(folder))
        {
            throw new ArgumentException("Carpeta de imagen no válida.", nameof(folder));
        }

        var key = $"{folder}/{Guid.NewGuid():N}.{image.Extension}";
        var path = ResolvePath(key)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, image.Content, cancellationToken);
        return key;
    }

    public override Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (!ImageKeys.IsLegacy(key) && ResolvePath(key) is { } path && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ruta física de una clave propia; null si la clave no es válida o sale de la carpeta raíz.</summary>
    public string? ResolvePath(string key)
    {
        if (!ImageKeys.IsValidOwnKey(key))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(RootPath, key));
        return path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }

    protected override string GetOwnUrl(string key) => PublicPath + key;
}

internal sealed class DisabledImageStorage(StorageOptions options) : ImageStorageBase(options)
{
    public override bool CanSave => false;

    public override Task<string> SaveAsync(string folder, ValidatedImage image, CancellationToken cancellationToken) =>
        throw new StorageUnavailableException();

    public override Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override string? GetOwnUrl(string key) => null;
}

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddDespachaStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName)
            .Validate(o => o.Provider is null or "Local" or "Disabled", "Storage:Provider debe ser Local o Disabled.")
            .Validate(o => string.IsNullOrWhiteSpace(o.LegacyBaseUrl)
                || (Uri.TryCreate(o.LegacyBaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)),
                "Storage:LegacyBaseUrl debe ser una URL absoluta.")
            .ValidateOnStart();

        services.AddSingleton<IImageStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var runtime = sp.GetRequiredService<DespachaRuntime>();
            var provider = options.Provider ?? (runtime.IsLocal ? "Local" : "Disabled");
            if (provider == "Disabled")
            {
                return new DisabledImageStorage(options);
            }

            var root = string.IsNullOrWhiteSpace(options.LocalRootPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Despacha", "media")
                : options.LocalRootPath;
            return new LocalImageStorage(options, root);
        });
        return services;
    }

    /// <summary>Solo con el adaptador local. Las imágenes del catálogo son públicas, igual que en el PHP; las claves son aleatorias.</summary>
    public static IEndpointRouteBuilder MapDespachaMedia(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LocalImageStorage.PublicPath + "{**key}", (string key, IImageStorage storage, HttpContext http) =>
        {
            if (storage is not LocalImageStorage local || local.ResolvePath(key) is not { } path || !File.Exists(path))
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(path, ImageValidation.ContentTypeOf(key));
        }).AllowAnonymous().ExcludeFromDescription();
        return endpoints;
    }
}
